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

        int total = 0, success = 0, failed = 0, warnings = 0;
        List<string> errors = [];
        // Chỉ đẩy NextSyncAt lên (theo tần suất cấu hình) khi lượt chạy hoàn tất bình thường — kể cả
        // khi status cuối là Failed do 0/n item thành công, đó vẫn là 1 lượt đã thử xong. Khi rơi vào
        // catch (lỗi hệ thống ngoài dự kiến, ví dụ mất kết nối PMIS ngay từ bước fetch) thì KHÔNG đẩy,
        // để tick 1 phút kế tiếp thử lại ngay thay vì phải chờ hết nguyên 1 chu kỳ tần suất.
        var completedNormally = false;
        try
        {
            (total, success, failed, warnings, errors) = objectType switch
            {
                SyncObjectType.Substation => await RunSubstationAsync(historyId),
                SyncObjectType.TransmissionLine => await RunLineAsync(historyId),
                SyncObjectType.Equipment => await RunEquipmentAsync(historyId),
                _ => (0, 0, 0, 0, [])
            };

            var status = total > 0 && success == 0
                ? SyncHistoryStatus.Failed
                : (warnings > 0 ? SyncHistoryStatus.Warning : SyncHistoryStatus.Success);
            await _syncHistoryRepository.CompleteAsync(historyId, status, total, success, failed,
                errors.Count > 0 ? string.Join("; ", errors.Take(5)) : null);
            completedNormally = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisScheduledSyncJob: đồng bộ tự động {ObjectType} thất bại.", objectType);
            await _syncHistoryRepository.CompleteAsync(historyId, SyncHistoryStatus.Failed, total, success, failed, ex.Message);
        }

        if (completedNormally)
        {
            var nextSyncAt = now.Add(ToTimeSpan(config.FrequencyValue, config.FrequencyUnit));
            await _syncConfigRepository.UpdateRunResultAsync(objectType, now, nextSyncAt);
        }
    }

    /// <summary>
    /// Đẩy 1 trang dữ liệu sang EquipmentService ngay khi vừa fetch xong, thay vì dồn hết các trang
    /// vào 1 danh sách khổng lồ rồi mới gửi 1 request duy nhất ở cuối — tránh request quá lớn dễ vượt
    /// timeout, và cách ly lỗi theo trang: 1 trang lỗi (PMIS/EquipmentService tạm gián đoạn) chỉ làm
    /// trang đó tính failed, các trang trước đã ghi SyncHistoryDetail xong vẫn giữ nguyên, các trang
    /// sau vẫn tiếp tục thử.
    /// </summary>
    private async Task<(int Success, int Failed, int Warnings, List<string> Errors)> PushPageAsync(
        Func<string, IReadOnlyList<JsonElement>, Task<(int Success, int Failed, int Warnings, List<string> Errors)>> pushPage,
        string historyId, List<JsonElement> pageItems, string pageLabel)
    {
        try
        {
            return await pushPage(historyId, pageItems);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisScheduledSyncJob: lỗi khi đồng bộ 1 trang ({PageLabel}).", pageLabel);
            return (0, pageItems.Count, 0, [$"{pageLabel}: {ex.Message}"]);
        }
    }

    private async Task<(int Total, int Success, int Failed, int Warnings, List<string> Errors)> RunSubstationAsync(string historyId)
    {
        int total = 0, success = 0, failed = 0, warnings = 0;
        var errors = new List<string>();
        var skip = 0;
        for (var page = 0; page < MaxPages; page++)
        {
            var result = await _pmisClient.GetSubstationsAsync(new PmisSubstationSearchRequest { Skip = skip, Take = PageSize });
            var pageItems = result.Items.Select(i => JsonSerializer.SerializeToElement(i)).ToList();
            total += pageItems.Count;

            var (pageSuccess, pageFailed, pageWarnings, pageErrors) = await PushPageAsync(
                (id, items) => _executionService.SyncInfrastructureAsync(1, id, items), historyId, pageItems, $"Trạm biến áp skip={skip}");
            success += pageSuccess;
            failed += pageFailed;
            warnings += pageWarnings;
            errors.AddRange(pageErrors);

            if (result.Items.Count < PageSize || total >= result.Total) break;
            skip += PageSize;
        }

        return (total, success, failed, warnings, errors);
    }

    private async Task<(int Total, int Success, int Failed, int Warnings, List<string> Errors)> RunLineAsync(string historyId)
    {
        int total = 0, success = 0, failed = 0, warnings = 0;
        var errors = new List<string>();
        var skip = 0;
        for (var page = 0; page < MaxPages; page++)
        {
            var result = await _pmisClient.GetLinesAsync(new PmisLineSearchRequest { Skip = skip, Take = PageSize });
            var pageItems = result.Items.Select(i => JsonSerializer.SerializeToElement(i)).ToList();
            total += pageItems.Count;

            var (pageSuccess, pageFailed, pageWarnings, pageErrors) = await PushPageAsync(
                (id, items) => _executionService.SyncInfrastructureAsync(2, id, items), historyId, pageItems, $"Đường dây skip={skip}");
            success += pageSuccess;
            failed += pageFailed;
            warnings += pageWarnings;
            errors.AddRange(pageErrors);

            if (result.Items.Count < PageSize || total >= result.Total) break;
            skip += PageSize;
        }

        return (total, success, failed, warnings, errors);
    }

    private async Task<(int Total, int Success, int Failed, int Warnings, List<string> Errors)> RunEquipmentAsync(string historyId)
    {
        // Thiết bị không có API "lấy tất cả" — phải lặp theo từng Trạm/Đường dây đã đồng bộ trước đó
        // (module 3) để lấy thiết bị con, đúng theo 2 API riêng biệt của tài liệu PMIS.
        var parents = await _equipmentServiceClient.GetSyncedInfrastructurePmisCodesAsync();
        int total = 0, success = 0, failed = 0, warnings = 0;
        var errors = new List<string>();

        foreach (var parent in parents)
        {
            var skip = 0;
            for (var page = 0; page < MaxPages; page++)
            {
                List<JsonElement> pageItems;
                int pageCount;
                if (parent.InfraTypeId == 1)
                {
                    var result = await _pmisClient.GetSubstationDevicesAsync(new PmisSubstationDeviceSearchRequest
                    {
                        MaTBA = parent.PmisCode,
                        Skip = skip,
                        Take = PageSize
                    });
                    pageItems = result.Items.Select(i => JsonSerializer.SerializeToElement(i)).ToList();
                    pageCount = result.Items.Count;
                }
                else
                {
                    var result = await _pmisClient.GetLineDevicesAsync(new PmisLineDeviceSearchRequest
                    {
                        MaDuongDay = parent.PmisCode,
                        KemQRCode = true, // chạy nền không có người quyết định — luôn lấy đầy đủ dữ liệu kể cả QR
                        Skip = skip,
                        Take = PageSize
                    });
                    pageItems = result.Items.Select(i => JsonSerializer.SerializeToElement(i)).ToList();
                    pageCount = result.Items.Count;
                }

                total += pageItems.Count;
                var (pageSuccess, pageFailed, pageWarnings, pageErrors) = await PushPageAsync(
                    _executionService.SyncEquipmentAsync, historyId, pageItems, $"Thiết bị cha={parent.PmisCode} skip={skip}");
                success += pageSuccess;
                failed += pageFailed;
                warnings += pageWarnings;
                errors.AddRange(pageErrors);

                if (pageCount < PageSize || pageCount == 0) break;
                skip += PageSize;
            }
        }

        return (total, success, failed, warnings, errors);
    }

    private static TimeSpan ToTimeSpan(int value, string unit) => unit switch
    {
        "MINUTE" => TimeSpan.FromMinutes(value),
        "HOUR" => TimeSpan.FromHours(value),
        "DAY" => TimeSpan.FromDays(value),
        _ => TimeSpan.FromMinutes(value)
    };
}
