using System.Text.Json;
using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Models.Pmis;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Services;
using Quartz;
using RedLockNet;
using Serilog;

namespace EvnHanoi.SyncService.Schedulers;

/// <summary>
/// Module 1+2 — thay PmisSyncScheduler cũ (chỉ log, chưa lưu gì). Tick mỗi phút, với
/// mỗi đối tượng (Trạm/Đường dây/Thiết bị) đang bật và đã tới hạn (NextSyncAt &lt;= now hoặc chưa
/// từng chạy), khoá RedLock riêng theo đối tượng rồi đồng bộ toàn bộ (phân trang) — "chọn tất cả",
/// khác với đồng bộ thủ công (người dùng tự chọn qua checkbox).
/// </summary>
public class PmisScheduledSyncJob : IJob
{
    private const int PageSize = 100;
    private const int MaxPages = 100; // an toàn: tối đa 10.000 bản ghi/đối tượng/lần chạy

    private readonly ISyncConfigRepository _syncConfigRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IPmisClient _pmisClient;
    private readonly IEquipmentServiceClient _equipmentServiceClient;
    private readonly IPmisSyncExecutionService _executionService;
    private readonly IDistributedLockFactory _lockFactory;

    public PmisScheduledSyncJob(
        ISyncConfigRepository syncConfigRepository,
        ISyncHistoryRepository syncHistoryRepository,
        IPmisClient pmisClient,
        IEquipmentServiceClient equipmentServiceClient,
        IPmisSyncExecutionService executionService,
        IDistributedLockFactory lockFactory)
    {
        _syncConfigRepository = syncConfigRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _pmisClient = pmisClient;
        _equipmentServiceClient = equipmentServiceClient;
        _executionService = executionService;
        _lockFactory = lockFactory;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        foreach (var objectType in new[] { SyncObjectType.Substation, SyncObjectType.TransmissionLine, SyncObjectType.Equipment })
        {
            try
            {
                await RunIfDueAsync(objectType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PmisScheduledSyncJob: lỗi không mong đợi khi xử lý đối tượng {ObjectType}", objectType);
            }
        }
    }

    private async Task RunIfDueAsync(string objectType)
    {
        var config = await _syncConfigRepository.GetByObjectTypeAsync(objectType);
        if (config == null || !config.IsEnabled) return;

        var now = DateTime.UtcNow;
        var isDue = config.NextSyncAt == null || config.NextSyncAt <= now;
        if (!isDue) return;

        await using var redLock = await _lockFactory.CreateLockAsync(
            $"sync:lock:pmis:{objectType}", TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));
        if (!redLock.IsAcquired)
        {
            Log.Information("PmisScheduledSyncJob: {ObjectType} đang được đồng bộ ở tiến trình khác, bỏ qua lượt này.", objectType);
            return;
        }

        var historyId = await _syncHistoryRepository.CreateAsync(new SyncHistory
        {
            SyncConfigId = config.Id,
            ObjectType = objectType,
            SyncType = SyncType.Auto,
            StartTime = now,
            Status = SyncHistoryStatus.Running,
            CreatedBy = "SYSTEM"
        });

        int total = 0, success = 0, failed = 0;
        List<string> errors = [];
        try
        {
            (total, success, failed, errors) = objectType switch
            {
                SyncObjectType.Substation => await RunSubstationAsync(historyId),
                SyncObjectType.TransmissionLine => await RunLineAsync(historyId),
                SyncObjectType.Equipment => await RunEquipmentAsync(historyId),
                _ => (0, 0, 0, [])
            };

            var status = total > 0 && success == 0 ? SyncHistoryStatus.Failed : SyncHistoryStatus.Success;
            await _syncHistoryRepository.CompleteAsync(historyId, status, total, success, failed,
                errors.Count > 0 ? string.Join("; ", errors.Take(5)) : null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisScheduledSyncJob: đồng bộ tự động {ObjectType} thất bại.", objectType);
            await _syncHistoryRepository.CompleteAsync(historyId, SyncHistoryStatus.Failed, total, success, failed, ex.Message);
        }

        var nextSyncAt = now.Add(ToTimeSpan(config.FrequencyValue, config.FrequencyUnit));
        await _syncConfigRepository.UpdateRunResultAsync(objectType, now, nextSyncAt);
    }

    private async Task<(int Total, int Success, int Failed, List<string> Errors)> RunSubstationAsync(string historyId)
    {
        var allItems = new List<JsonElement>();
        var skip = 0;
        for (var page = 0; page < MaxPages; page++)
        {
            var result = await _pmisClient.GetSubstationsAsync(new PmisSubstationSearchRequest { Skip = skip, Take = PageSize });
            allItems.AddRange(result.Items.Select(i => JsonSerializer.SerializeToElement(i)));
            if (result.Items.Count < PageSize || allItems.Count >= result.Total) break;
            skip += PageSize;
        }

        var (success, failed, errors) = await _executionService.SyncInfrastructureAsync(1, historyId, allItems);
        return (allItems.Count, success, failed, errors);
    }

    private async Task<(int Total, int Success, int Failed, List<string> Errors)> RunLineAsync(string historyId)
    {
        var allItems = new List<JsonElement>();
        var skip = 0;
        for (var page = 0; page < MaxPages; page++)
        {
            var result = await _pmisClient.GetLinesAsync(new PmisLineSearchRequest { Skip = skip, Take = PageSize });
            allItems.AddRange(result.Items.Select(i => JsonSerializer.SerializeToElement(i)));
            if (result.Items.Count < PageSize || allItems.Count >= result.Total) break;
            skip += PageSize;
        }

        var (success, failed, errors) = await _executionService.SyncInfrastructureAsync(2, historyId, allItems);
        return (allItems.Count, success, failed, errors);
    }

    private async Task<(int Total, int Success, int Failed, List<string> Errors)> RunEquipmentAsync(string historyId)
    {
        // Thiết bị không có API "lấy tất cả" — phải lặp theo từng Trạm/Đường dây đã đồng bộ trước đó
        // (module 3) để lấy thiết bị con, đúng theo 2 API riêng biệt của tài liệu PMIS.
        var parents = await _equipmentServiceClient.GetSyncedInfrastructurePmisCodesAsync();
        var allItems = new List<JsonElement>();

        foreach (var parent in parents)
        {
            var skip = 0;
            for (var page = 0; page < MaxPages; page++)
            {
                if (parent.InfraTypeId == 1)
                {
                    var result = await _pmisClient.GetSubstationDevicesAsync(new PmisSubstationDeviceSearchRequest
                    {
                        MaTBA = parent.PmisCode,
                        Skip = skip,
                        Take = PageSize
                    });
                    allItems.AddRange(result.Items.Select(i => JsonSerializer.SerializeToElement(i)));
                    if (result.Items.Count < PageSize || result.Items.Count == 0) break;
                }
                else
                {
                    var result = await _pmisClient.GetLineDevicesAsync(new PmisLineDeviceSearchRequest
                    {
                        MaDuongDay = parent.PmisCode,
                        Skip = skip,
                        Take = PageSize
                    });
                    allItems.AddRange(result.Items.Select(i => JsonSerializer.SerializeToElement(i)));
                    if (result.Items.Count < PageSize || result.Items.Count == 0) break;
                }

                skip += PageSize;
            }
        }

        var (success, failed, errors) = await _executionService.SyncEquipmentAsync(historyId, allItems);
        return (allItems.Count, success, failed, errors);
    }

    private static TimeSpan ToTimeSpan(int value, string unit) => unit switch
    {
        "MINUTE" => TimeSpan.FromMinutes(value),
        "HOUR" => TimeSpan.FromHours(value),
        "DAY" => TimeSpan.FromDays(value),
        _ => TimeSpan.FromMinutes(value)
    };
}
