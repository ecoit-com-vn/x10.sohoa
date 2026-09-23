using System.Text.Json;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Infrastructure.Messaging;
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
    // An toàn: tối đa bản ghi/đối tượng (hoặc /cha, ở Thiết bị)/lần chạy — tính theo TỔNG SỐ BẢN GHI,
    // không phải số TRANG, vì PageSize giờ admin tự cấu hình được qua "Cấu hình kết nối API" (trước đây là
    // hằng số cố định MaxPages=50 × PageSize cố định=1000 = 50.000; nếu vẫn dùng số trang cố định làm giới
    // hạn, admin chỉnh PageSize xuống thấp (vd 100) sẽ vô tình siết giới hạn thật xuống còn 50×100=5.000 —
    // ÍT HƠN dữ liệu PMIS thật (đã gặp: 24.429 trạm biến áp, 13.681+ đường dây), khiến mỗi lượt đồng bộ âm
    // thầm dừng giữa chừng, KHÔNG BAO GIỜ đồng bộ hết vì skip luôn reset về 0 lượt sau). Dùng chung đúng 1
    // nguồn (PmisPaging.MaxTotalRecordsPerRun) với DocumentMaxTotalRecords ở PmisSyncExecutionService,
    // tránh định nghĩa lặp ở 2 nơi dễ lệch nhau khi cần đổi ngưỡng sau này.
    private const int MaxTotalRecords = PmisPaging.MaxTotalRecordsPerRun;

    private readonly ISyncConfigRepository _syncConfigRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IPmisClient _pmisClient;
    private readonly IEquipmentServiceClient _equipmentServiceClient;
    private readonly IPmisSyncExecutionService _executionService;
    private readonly IDistributedLockFactory _lockFactory;
    private readonly IMessageProducer _messageProducer;
    private readonly IPmisEndpointConfigProvider _endpointConfigProvider;

    public PmisScheduledSyncJob(
        ISyncConfigRepository syncConfigRepository,
        ISyncHistoryRepository syncHistoryRepository,
        IPmisClient pmisClient,
        IEquipmentServiceClient equipmentServiceClient,
        IPmisSyncExecutionService executionService,
        IDistributedLockFactory lockFactory,
        IMessageProducer messageProducer,
        IPmisEndpointConfigProvider endpointConfigProvider)
    {
        _syncConfigRepository = syncConfigRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _pmisClient = pmisClient;
        _equipmentServiceClient = equipmentServiceClient;
        _executionService = executionService;
        _lockFactory = lockFactory;
        _messageProducer = messageProducer;
        _endpointConfigProvider = endpointConfigProvider;
    }

    /// <summary>Kiểm tra + xử lý (log Warning, tăng warnings) khi 1 vòng phân trang chạm giới hạn an toàn
    /// tổng số bản ghi — dùng chung cho cả 3 vòng lặp Trạm biến áp/Đường dây/Thiết bị bên dưới, tránh lặp
    /// lại y hệt 1 khối code chỉ khác mỗi nhãn đối tượng.</summary>
    private static bool HasHitSafetyCap(int skip, string entityLabel, ref int warnings)
    {
        if (skip < MaxTotalRecords) return false;
        Log.Warning("PmisScheduledSyncJob: {Entity} đã đạt giới hạn an toàn {Max} bản ghi/lượt chạy, dừng lại dù PMIS có thể còn dữ liệu (skip={Skip}) — sẽ tiếp tục ở lượt sau.", entityLabel, MaxTotalRecords, skip);
        warnings++;
        return true;
    }

    /// <summary>Số bản ghi/trang admin đã cấu hình cho apiCode này qua "Cấu hình kết nối API" — mặc định
    /// <see cref="Models.PmisPaging.DefaultPageSize"/> nếu API chưa cấu hình/chưa bật (đọc từ cache 5 phút
    /// của <see cref="IPmisEndpointConfigProvider"/>, không tốn thêm round-trip DB đáng kể).</summary>
    private async Task<int> GetPageSizeAsync(string apiCode) =>
        (await _endpointConfigProvider.GetEndpointAsync(apiCode))?.PageSize ?? PmisPaging.DefaultPageSize;

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
        try
        {
            (total, success, failed, warnings, errors) = objectType switch
            {
                SyncObjectType.Substation => await RunSubstationAsync(historyId),
                SyncObjectType.TransmissionLine => await RunLineAsync(historyId),
                SyncObjectType.Equipment => await RunEquipmentAsync(historyId),
                _ => (0, 0, 0, 0, [])
            };

            var status = (total > 0 && success == 0) || (total == 0 && errors.Count > 0)
                ? SyncHistoryStatus.Failed
                : (warnings > 0 ? SyncHistoryStatus.Warning : SyncHistoryStatus.Success);
            var completed = await _syncHistoryRepository.CompleteAsync(historyId, status, total, success, failed,
                errors.Count > 0 ? string.Join("; ", errors.Take(5)) : null);
            if (!completed)
                Log.Warning("PmisScheduledSyncJob: {ObjectType} hoàn tất với kết quả thật ({Status}, total={Total}, success={Success}) nhưng syncHistoryId={SyncHistoryId} đã bị SyncHistoryWatchdogJob đánh FAILED trước đó (chạy quá 30 phút) — giữ nguyên FAILED của watchdog, bỏ kết quả thật này.", objectType, status, total, success, historyId);

            // Lượt chạy hoàn tất bình thường (kể cả Failed do 0/n item thành công vẫn là 1 lượt đã thử
            // xong) — đẩy NextSyncAt theo tần suất cấu hình, và reset bộ đếm lỗi liên tiếp vì PMIS đã
            // phản hồi được (dù dữ liệu bên trong có lỗi riêng lẻ hay không).
            var nextSyncAt = now.Add(ToTimeSpan(config.FrequencyValue, config.FrequencyUnit));
            await _syncConfigRepository.UpdateRunResultAsync(objectType, now, nextSyncAt, consecutiveFailureCount: 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisScheduledSyncJob: đồng bộ tự động {ObjectType} thất bại.", objectType);
            var completed = await _syncHistoryRepository.CompleteAsync(historyId, SyncHistoryStatus.Failed, total, success, failed, SyncErrorFormatter.Format(ex));
            if (!completed)
                Log.Warning("PmisScheduledSyncJob: syncHistoryId={SyncHistoryId} ({ObjectType}) đã bị SyncHistoryWatchdogJob đánh FAILED trước khi job tự báo lỗi xong — exception thật xem log phía trên.", historyId, objectType);

            // Lỗi ngay từ bước gọi PMIS — đánh dấu thất bại ngay (không tự retry), và chờ đúng đến lần
            // kế tiếp theo tần suất đã cấu hình mới thử lại, giống hệt nhánh thành công — không rút
            // ngắn chu kỳ. Chỉ cảnh báo admin đúng 1 lần khi chạm ngưỡng FailureNotifyThreshold.
            var newFailureCount = config.ConsecutiveFailureCount + 1;
            var nextSyncAtOnFailure = now.Add(ToTimeSpan(config.FrequencyValue, config.FrequencyUnit));
            await _syncConfigRepository.UpdateRunResultAsync(objectType, now, nextSyncAtOnFailure, newFailureCount);

            if (newFailureCount == FailureNotifyThreshold)
                await PublishSyncFailedNotificationAsync(objectType, newFailureCount, SyncErrorFormatter.FormatShort(ex));
        }
    }

    private const int FailureNotifyThreshold = 5;

    private async Task PublishSyncFailedNotificationAsync(string objectType, int failureCount, string? lastError)
    {
        try
        {
            await _messageProducer.PublishToExchangeAsync(
                new PmisSyncFailedEvent
                {
                    ObjectType = objectType,
                    ConsecutiveFailureCount = failureCount,
                    LastErrorMessage = lastError,
                    Timestamp = DateTime.UtcNow
                },
                NotificationTopicTopology.ExchangeName,
                NotificationTopicTopology.PmisSyncFailedRoutingKey);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PmisScheduledSyncJob: lỗi khi publish cảnh báo đồng bộ PMIS thất bại liên tục.");
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
            return (0, pageItems.Count, 0, [$"{pageLabel}: {SyncErrorFormatter.FormatShort(ex)}"]);
        }
    }

    private async Task<(int Total, int Success, int Failed, int Warnings, List<string> Errors)> RunSubstationAsync(string historyId)
    {
        var pageSize = await GetPageSizeAsync("SUBSTATION_LIST");
        int total = 0, success = 0, failed = 0, warnings = 0;
        var errors = new List<string>();
        var skip = 0;
        while (true)
        {
            PmisListResponse<PmisSubstationDto> result;
            try
            {
                result = await _pmisClient.GetSubstationsAsync(new PmisSubstationSearchRequest { Skip = skip, Take = pageSize });
            }
            catch (Exception ex)
            {
                // Lỗi khi GỌI PMIS (khác lỗi khi lưu — đã cách ly riêng ở PushPageAsync) — không để lỗi
                // 1 trang làm mất kết quả các trang TRƯỚC đã lưu thành công; dừng phân trang tại đây,
                // các trang sau coi như chưa kịp lấy, sẽ tự thử lại ở lượt đồng bộ kế tiếp.
                Log.Error(ex, "PmisScheduledSyncJob: lỗi khi lấy danh sách Trạm biến áp (skip={Skip}).", skip);
                errors.Add($"Trạm biến áp skip={skip}: {SyncErrorFormatter.FormatShort(ex)}");
                break;
            }

            var pageItems = result.Items.Select(i => JsonSerializer.SerializeToElement(i)).ToList();
            total += pageItems.Count;

            var (pageSuccess, pageFailed, pageWarnings, pageErrors) = await PushPageAsync(
                (id, items) => _executionService.SyncInfrastructureAsync(1, id, items), historyId, pageItems, $"Trạm biến áp skip={skip}");
            success += pageSuccess;
            failed += pageFailed;
            warnings += pageWarnings;
            errors.AddRange(pageErrors);

            if (result.Items.Count < pageSize || total >= result.Total) break;
            skip += pageSize;

            if (HasHitSafetyCap(skip, "Trạm biến áp", ref warnings)) break;
        }

        return (total, success, failed, warnings, errors);
    }

    private async Task<(int Total, int Success, int Failed, int Warnings, List<string> Errors)> RunLineAsync(string historyId)
    {
        var pageSize = await GetPageSizeAsync("LINE_LIST");
        int total = 0, success = 0, failed = 0, warnings = 0;
        var errors = new List<string>();
        var skip = 0;
        while (true)
        {
            PmisListResponse<PmisLineDto> result;
            try
            {
                result = await _pmisClient.GetLinesAsync(new PmisLineSearchRequest { Skip = skip, Take = pageSize });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PmisScheduledSyncJob: lỗi khi lấy danh sách Đường dây (skip={Skip}).", skip);
                errors.Add($"Đường dây skip={skip}: {SyncErrorFormatter.FormatShort(ex)}");
                break;
            }

            var pageItems = result.Items.Select(i => JsonSerializer.SerializeToElement(i)).ToList();
            total += pageItems.Count;

            var (pageSuccess, pageFailed, pageWarnings, pageErrors) = await PushPageAsync(
                (id, items) => _executionService.SyncInfrastructureAsync(2, id, items), historyId, pageItems, $"Đường dây skip={skip}");
            success += pageSuccess;
            failed += pageFailed;
            warnings += pageWarnings;
            errors.AddRange(pageErrors);

            if (result.Items.Count < pageSize || total >= result.Total) break;
            skip += pageSize;

            if (HasHitSafetyCap(skip, "Đường dây", ref warnings)) break;
        }

        // Cha (PARENT_ID) của mỗi Đường dây được tra TRỰC TIẾP theo mã PMIS "maCha" ngay trong lượt sync
        // này (xem InfrastructureRepository.UpsertFromPmisAsync) — không cần job nền riêng nào để "khớp
        // lại" nữa; chỉ "lỡ nhịp" khi trục CHƯA từng được đồng bộ tới, và tự khớp đúng ở lượt kế tiếp
        // (PMIS trả toàn bộ dữ liệu mỗi lượt, không phải delta).
        return (total, success, failed, warnings, errors);
    }

    private async Task<(int Total, int Success, int Failed, int Warnings, List<string> Errors)> RunEquipmentAsync(string historyId)
    {
        // Thiết bị không có API "lấy tất cả" — phải lặp theo từng Trạm/Đường dây đã đồng bộ trước đó
        // (module 3) để lấy thiết bị con, đúng theo 2 API riêng biệt của tài liệu PMIS.
        var parents = await _equipmentServiceClient.GetSyncedInfrastructurePmisCodesAsync();
        var substationDevicePageSize = await GetPageSizeAsync("SUBSTATION_DEVICE_LIST");
        var lineDevicePageSize = await GetPageSizeAsync("LINE_DEVICE_LIST");
        int total = 0, success = 0, failed = 0, warnings = 0;
        var errors = new List<string>();

        foreach (var parent in parents)
        {
            var pageSize = parent.InfraTypeId == 1 ? substationDevicePageSize : lineDevicePageSize;
            var skip = 0;
            while (true)
            {
                List<JsonElement> pageItems;
                int pageCount;
                try
                {
                    if (parent.InfraTypeId == 1)
                    {
                        var result = await _pmisClient.GetSubstationDevicesAsync(new PmisSubstationDeviceSearchRequest
                        {
                            MaTBA = parent.PmisCode,
                            Skip = skip,
                            Take = pageSize
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
                            Take = pageSize
                        });
                        pageItems = result.Items.Select(i => JsonSerializer.SerializeToElement(i)).ToList();
                        pageCount = result.Items.Count;
                    }
                }
                catch (Exception ex)
                {
                    // Lỗi khi GỌI PMIS cho ĐÚNG 1 trạm/đường dây cha (vd. timeout, PMIS lỗi tạm thời) —
                    // TRƯỚC ĐÂY exception này văng thẳng ra ngoài foreach, làm cả lượt EQUIPMENT coi là
                    // Failed và bỏ dở TẤT CẢ trạm/đường dây cha còn lại chưa xử lý tới (vd. nếu đường dây
                    // đứng sau trạm biến áp trong danh sách cha, 1 trạm lỗi là không bao giờ chạm tới
                    // thiết bị đường dây). Giờ chỉ ghi nhận lỗi cho riêng cha này rồi sang cha tiếp theo.
                    Log.Error(ex, "PmisScheduledSyncJob: lỗi khi lấy danh sách thiết bị cho {PmisCode} (skip={Skip}), bỏ qua, tiếp tục các trạm/đường dây khác.", parent.PmisCode, skip);
                    errors.Add($"Thiết bị cha={parent.PmisCode} skip={skip}: {SyncErrorFormatter.FormatShort(ex)}");
                    break;
                }

                total += pageItems.Count;
                var (pageSuccess, pageFailed, pageWarnings, pageErrors) = await PushPageAsync(
                    (hId, items) => _executionService.SyncEquipmentAsync(hId, items, parent.PmisCode),
                    historyId, pageItems, $"Thiết bị cha={parent.PmisCode} skip={skip}");
                success += pageSuccess;
                failed += pageFailed;
                warnings += pageWarnings;
                errors.AddRange(pageErrors);

                if (pageCount < pageSize || pageCount == 0) break;
                skip += pageSize;

                if (HasHitSafetyCap(skip, $"Thiết bị cha={parent.PmisCode}", ref warnings)) break;
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
