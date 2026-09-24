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
///
/// MỖI OBJECT TYPE LÀ 1 JOB INSTANCE RIÊNG (JobKey khác nhau, cùng class này) — xem đăng ký ở
/// Program.cs, mỗi JobDetail mang đúng 1 <see cref="ObjectTypeDataKey"/> trong JobDataMap. TRƯỚC ĐÂY 1
/// Execute() duy nhất lặp tuần tự cả 3 loại trong CÙNG 1 lần gọi — nếu Thiết bị chạy nhiều giờ (đã xảy
/// ra thật, xem MaxParentsPerRun bên dưới), Trạm/Đường dây bị "đói" theo lịch suốt thời gian đó vì
/// [DisallowConcurrentExecution] chặn Execute() mới của CÙNG JobKey khởi động — kể cả khi Trạm/Đường dây
/// đã tới hạn từ lâu. Tách JobKey khiến [DisallowConcurrentExecution] (vốn scope theo JobKey, không
/// phải theo class) chỉ chặn đúng 1 loại đang chạy, không ảnh hưởng 2 loại còn lại — khớp với RedLock
/// vốn đã tách theo objectType từ trước (nay 2 cơ chế nhất quán ở cùng 1 mức chi tiết).
///
/// [DisallowConcurrentExecution]: phòng thủ chiều sâu, KHÔNG phải cơ chế chính chống chạy trùng (đó là
/// RedLock theo objectType — bảo vệ cả liên-pod lẫn trong-1-pod, xem comment tại nơi tạo redLock bên dưới).
/// Chặn Quartz tự khởi 1 Execute() MỚI đè lên Execute() đang chạy trong CÙNG 1 pod cho CÙNG objectType
/// nếu 1 lượt tick mất hơn 1 phút — tránh lãng phí round-trip DB kiểm tra isDue/lock 2 lần cùng lúc, dù
/// không có tác dụng phụ sai lệch dữ liệu nếu thiếu (RedLock đã đủ để bảo vệ đúng).
/// </summary>
[DisallowConcurrentExecution]
public class PmisScheduledSyncJob : IJob
{
    /// <summary>Key trong JobDataMap của mỗi JobDetail (xem Program.cs) chỉ định job instance này chịu
    /// trách nhiệm cho đúng 1 <see cref="SyncObjectType"/> nào — KHÔNG còn lặp cả 3 loại trong 1 Execute().</summary>
    public const string ObjectTypeDataKey = "ObjectType";

    // An toàn: tối đa bản ghi/đối tượng (hoặc /cha, ở Thiết bị)/lần chạy — tính theo TỔNG SỐ BẢN GHI,
    // không phải số TRANG, vì PageSize giờ admin tự cấu hình được qua "Cấu hình kết nối API" (trước đây là
    // hằng số cố định MaxPages=50 × PageSize cố định=1000 = 50.000; nếu vẫn dùng số trang cố định làm giới
    // hạn, admin chỉnh PageSize xuống thấp (vd 100) sẽ vô tình siết giới hạn thật xuống còn 50×100=5.000 —
    // ÍT HƠN dữ liệu PMIS thật (đã gặp: 24.429 trạm biến áp, 13.681+ đường dây), khiến mỗi lượt đồng bộ âm
    // thầm dừng giữa chừng, KHÔNG BAO GIỜ đồng bộ hết vì skip luôn reset về 0 lượt sau). Dùng chung đúng 1
    // nguồn (PmisPaging.MaxTotalRecordsPerRun) với DocumentMaxTotalRecords ở PmisSyncExecutionService,
    // tránh định nghĩa lặp ở 2 nơi dễ lệch nhau khi cần đổi ngưỡng sau này.
    private const int MaxTotalRecords = PmisPaging.MaxTotalRecordsPerRun;

    // An toàn RIÊNG cho vòng lặp Thiết bị: MaxTotalRecords (bản ghi) không đủ chặn thời lượng chạy vì
    // phần lớn cha (Trạm/Đường dây nhỏ) trả về RẤT ÍT/0 thiết bị — vòng foreach vẫn phải round-trip PMIS
    // tuần tự cho MỖI cha trong ~38.000 cha dù tổng bản ghi thu về thấp, nên trần theo SỐ CHA đã thăm mới
    // thật sự chặn được thời lượng 1 lượt chạy (đã quan sát thật trên production: lượt Equipment chạy
    // 7+ giờ). 4000 cha × ước lượng &lt;1s/cha (network round-trip PMIS thật) ≈ dưới 60-70 phút, để lại
    // biên an toàn trước ngưỡng watchdog (SyncHistoryWatchdogJob.StaleThreshold = 60 phút) mà không cắt
    // quá sâu số cha xử lý được mỗi lượt.
    private const int MaxParentsPerRunEquipment = 4000;

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
    /// lại y hệt 1 khối code chỉ khác mỗi nhãn đối tượng. NHẬN <paramref name="processedThisRun"/> (số bản
    /// ghi ĐÃ XỬ LÝ TRONG LƯỢT NÀY, không phải vị trí "skip" tuyệt đối) — kể từ khi có cơ chế resume
    /// (SyncConfig.SyncCursor), 1 lượt có thể BẮT ĐẦU từ 1 skip đã lớn sẵn (tiếp tục lượt trước), nên so
    /// sánh skip tuyệt đối với trần sẽ khiến lượt resume dừng ngay lập tức dù chưa xử lý được bản ghi nào.</summary>
    private static bool HasHitSafetyCap(int processedThisRun, string entityLabel, ref int warnings)
    {
        if (processedThisRun < MaxTotalRecords) return false;
        Log.Warning("PmisScheduledSyncJob: {Entity} đã đạt giới hạn an toàn {Max} bản ghi/lượt chạy, dừng lại dù PMIS có thể còn dữ liệu (đã xử lý {Processed} bản ghi lượt này) — sẽ tiếp tục ở lượt sau.", entityLabel, MaxTotalRecords, processedThisRun);
        warnings++;
        return true;
    }

    /// <summary>Xoay vòng danh sách Trạm/Đường dây cha để BẮT ĐẦU ngay sau <paramref name="cursor"/> (mã
    /// PMIS của cha nơi lượt trước dùng hết ngân sách gọi PMIS thật) thay vì luôn bắt đầu lại từ đầu danh
    /// sách mỗi lượt — nếu không, các cha ở cuối danh sách sẽ không bao giờ được ưu tiên ngân sách (xem
    /// PmisSyncExecutionService.MaxEquipmentDetailCallsPerRun). Cursor không còn tồn tại (cha đã bị xoá
    /// giữa 2 lượt) hoặc null/rỗng → giữ nguyên thứ tự gốc, bắt đầu lại từ đầu.</summary>
    private static List<SyncedInfrastructurePmisCode> RotateParentsByCursor(List<SyncedInfrastructurePmisCode> parents, string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return parents;
        var idx = parents.FindIndex(p => p.PmisCode == cursor);
        if (idx < 0 || idx + 1 >= parents.Count) return parents;
        return parents.Skip(idx + 1).Concat(parents.Take(idx + 1)).ToList();
    }

    /// <summary>Số bản ghi/trang admin đã cấu hình cho apiCode này qua "Cấu hình kết nối API" — mặc định
    /// <see cref="Models.PmisPaging.DefaultPageSize"/> nếu API chưa cấu hình/chưa bật (đọc từ cache 5 phút
    /// của <see cref="IPmisEndpointConfigProvider"/>, không tốn thêm round-trip DB đáng kể).</summary>
    private async Task<int> GetPageSizeAsync(string apiCode) =>
        (await _endpointConfigProvider.GetEndpointAsync(apiCode))?.PageSize ?? PmisPaging.DefaultPageSize;

    public async Task Execute(IJobExecutionContext context)
    {
        var objectType = context.MergedJobDataMap.GetString(ObjectTypeDataKey);
        if (string.IsNullOrEmpty(objectType))
        {
            // Không có JobDataMap (không nên xảy ra với cách đăng ký ở Program.cs) — phòng thủ, xử lý
            // như trước đây (cả 3 loại tuần tự) thay vì im lặng không làm gì.
            Log.Warning("PmisScheduledSyncJob: JobDetail thiếu {Key} trong JobDataMap, chạy tuần tự cả 3 loại (fallback).", ObjectTypeDataKey);
            foreach (var fallbackType in new[] { SyncObjectType.Substation, SyncObjectType.TransmissionLine, SyncObjectType.Equipment })
            {
                try { await RunIfDueAsync(fallbackType); }
                catch (Exception ex) { Log.Error(ex, "PmisScheduledSyncJob: lỗi không mong đợi khi xử lý đối tượng {ObjectType}", fallbackType); }
            }
            return;
        }

        try
        {
            await RunIfDueAsync(objectType);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisScheduledSyncJob: lỗi không mong đợi khi xử lý đối tượng {ObjectType}", objectType);
        }
    }

    private async Task RunIfDueAsync(string objectType)
    {
        var config = await _syncConfigRepository.GetByObjectTypeAsync(objectType);
        if (config == null || !config.IsEnabled) return;

        var now = DateTime.UtcNow;
        var isDue = config.NextSyncAt == null || config.NextSyncAt <= now;
        if (!isDue) return;

        // 10 phút ở đây KHÔNG phải trần thời lượng tối đa của 1 lượt chạy — RedLockNet.SERedis tự động gia
        // hạn (background keepalive timer, xem RedLockNet.SERedis nguồn: StartAutoExtendTimer) đều đặn
        // trong suốt thời gian object `redLock` này còn sống (tới khi DisposeAsync ở cuối using), miễn tiến
        // trình KHÔNG bị crash và Redis vẫn kết nối được — 1 lượt chạy hợp lệ dù kéo dài hàng chục phút vẫn
        // giữ được khoá liên tục, không có 2 lượt chạy trùng cho CÙNG objectType. TimeSpan này chỉ là thời
        // gian sống của MỖI lần gia hạn (nếu tiến trình crash giữa chừng, khoá tự hết hạn sau tối đa từng
        // này thời gian — không kẹt vĩnh viễn).
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
                SyncObjectType.Substation => await RunSubstationAsync(config, historyId),
                SyncObjectType.TransmissionLine => await RunLineAsync(config, historyId),
                SyncObjectType.Equipment => await RunEquipmentAsync(config, historyId),
                _ => (0, 0, 0, 0, [])
            };

            // errors.Count > 0 giờ LUÔN kéo status xuống ít nhất Warning, bất kể total/success — TRƯỚC ĐÂY
            // chỉ xét khi total == 0, nên 1 trang PMIS lỗi giữa chừng (Substation/Line "break" phân trang,
            // hoặc Equipment bỏ qua 1 cha lỗi) sau khi đã có ≥1 bản ghi lưu thành công sẽ rơi vào status
            // SUCCESS dù phần lớn dữ liệu lượt này chưa hề được lấy — bug thật đã gặp (xem code review).
            var status = (total > 0 && success == 0) || (total == 0 && errors.Count > 0)
                ? SyncHistoryStatus.Failed
                : (warnings > 0 || errors.Count > 0 ? SyncHistoryStatus.Warning : SyncHistoryStatus.Success);

            // total == 0 && không có lỗi/warning nào khác: PMIS/EquipmentService phản hồi "hợp lệ" (HTTP
            // 200, không exception) nhưng KHÔNG có 1 bản ghi nào — với mô hình "PMIS trả toàn bộ dataset
            // mỗi lượt" (full resync, không phải delta), 1 lượt 0-bản-ghi gần như luôn là DẤU HIỆU BẤT
            // THƯỜNG (danh sách cha rỗng do lỗi đọc dữ liệu khác, PMIS đổi quyền truy cập âm thầm, filter
            // phía PMIS lỗi trả rỗng thay vì lỗi...) — bài học thật: 1 bug khác (đọc PMIS_CODE) từng khiến
            // hàng loạt lượt Equipment báo "SUCCESS" nhiều ngày liền dù total=0, không ai phát hiện vì UI
            // hiện SUCCESS êm ru. Ép lên Warning ở ĐÂY (sau khi status đã tính ở trên, KHÔNG gộp vào
            // `errors` — nếu không sẽ tự kích luôn nhánh Failed phía trên vì total==0 && errors.Count>0)
            // để hiện rõ trên "Lịch sử đồng bộ", không lẫn vào các lượt thật sự ổn. Không dùng Failed vì
            // chưa chắc là LỖI (có thể môi trường mới chưa có dữ liệu).
            var zeroRecordsMessage = total == 0 && status == SyncHistoryStatus.Success
                ? $"{objectType}: PMIS trả về 0 bản ghi — bất thường với mô hình full-resync, kiểm tra kết nối/dữ liệu nguồn nếu lặp lại nhiều lượt."
                : null;
            if (zeroRecordsMessage != null)
            {
                status = SyncHistoryStatus.Warning;
                errors.Add(zeroRecordsMessage);
            }

            var completed = await _syncHistoryRepository.CompleteAsync(historyId, status, total, success, failed,
                errors.Count > 0 ? string.Join("; ", errors.Take(5)) : null);
            if (!completed)
                Log.Warning("PmisScheduledSyncJob: {ObjectType} hoàn tất với kết quả thật ({Status}, total={Total}, success={Success}) nhưng syncHistoryId={SyncHistoryId} đã bị SyncHistoryWatchdogJob đánh FAILED trước đó (chạy quá ngưỡng an toàn, xem SyncHistoryWatchdogJob.StaleThreshold) — giữ nguyên FAILED của watchdog, bỏ kết quả thật này.", objectType, status, total, success, historyId);

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

    private async Task<(int Total, int Success, int Failed, int Warnings, List<string> Errors)> RunSubstationAsync(SyncConfig config, string historyId)
    {
        var pageSize = await GetPageSizeAsync("SUBSTATION_LIST");
        int total = 0, success = 0, failed = 0, warnings = 0;
        var errors = new List<string>();
        // Tiếp tục từ vị trí lượt TRƯỚC dừng lại vì chạm trần an toàn (nếu có) thay vì luôn bắt đầu lại từ
        // 0 — xem SyncConfig.SyncCursor + Migration0011. Không có cursor (lượt trước hoàn tất trọn vẹn,
        // hoặc lượt trước lỗi gọi PMIS mà không chạm trần) → bắt đầu từ đầu như cũ.
        var skip = int.TryParse(config.SyncCursor, out var resumeSkip) && resumeSkip > 0 ? resumeSkip : 0;
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
                // các trang sau coi như chưa kịp lấy, sẽ tự thử lại ở lượt đồng bộ kế tiếp. KHÔNG đụng
                // SyncCursor ở đây — đây là lỗi TẠM THỜI (PMIS gián đoạn), không phải chạm trần an toàn;
                // giữ nguyên cursor đang có (có thể null, có thể đang dở từ lượt hit-cap trước đó).
                Log.Error(ex, "PmisScheduledSyncJob: lỗi khi lấy danh sách Trạm biến áp (skip={Skip}).", skip);
                errors.Add($"Trạm biến áp skip={skip}: {SyncErrorFormatter.FormatShort(ex)}");
                return await FinishSubstationOrLineAsync(SyncObjectType.Substation, 1, config, historyId, total, success, failed, warnings, errors);
            }

            var pageItems = result.Items.Select(i => JsonSerializer.SerializeToElement(i)).ToList();
            total += pageItems.Count;

            var (pageSuccess, pageFailed, pageWarnings, pageErrors) = await PushPageAsync(
                (id, items) => _executionService.SyncInfrastructureAsync(1, id, items, syncDocuments: false), historyId, pageItems, $"Trạm biến áp skip={skip}");
            success += pageSuccess;
            failed += pageFailed;
            warnings += pageWarnings;
            errors.AddRange(pageErrors);

            // So sánh theo VỊ TRÍ TUYỆT ĐỐI (skip + số bản ghi vừa lấy) với result.Total, KHÔNG phải biến
            // `total` (chỉ đếm số bản ghi ĐÃ XỬ LÝ TRONG LƯỢT NÀY) — vì lượt này có thể BẮT ĐẦU từ 1 skip
            // đã khác 0 (resume), nên `total` không còn phản ánh đúng vị trí trong toàn bộ danh sách PMIS.
            if (result.Items.Count < pageSize || skip + pageItems.Count >= result.Total)
            {
                // Quét trọn tới hết danh sách (từ vị trí resume trở đi) — coi là 1 vòng hoàn tất, xoá cursor
                // để lượt sau bắt đầu lại từ đầu danh sách.
                await _syncConfigRepository.UpdateSyncCursorAsync(SyncObjectType.Substation, null);
                return await FinishSubstationOrLineAsync(SyncObjectType.Substation, 1, config, historyId, total, success, failed, warnings, errors);
            }
            skip += pageSize;

            if (HasHitSafetyCap(total, "Trạm biến áp", ref warnings))
            {
                await _syncConfigRepository.UpdateSyncCursorAsync(SyncObjectType.Substation, skip.ToString());
                return await FinishSubstationOrLineAsync(SyncObjectType.Substation, 1, config, historyId, total, success, failed, warnings, errors);
            }
        }
    }

    private async Task<(int Total, int Success, int Failed, int Warnings, List<string> Errors)> RunLineAsync(SyncConfig config, string historyId)
    {
        var pageSize = await GetPageSizeAsync("LINE_LIST");
        int total = 0, success = 0, failed = 0, warnings = 0;
        var errors = new List<string>();
        var skip = int.TryParse(config.SyncCursor, out var resumeSkip) && resumeSkip > 0 ? resumeSkip : 0;
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
                return await FinishSubstationOrLineAsync(SyncObjectType.TransmissionLine, 2, config, historyId, total, success, failed, warnings, errors);
            }

            var pageItems = result.Items.Select(i => JsonSerializer.SerializeToElement(i)).ToList();
            total += pageItems.Count;

            var (pageSuccess, pageFailed, pageWarnings, pageErrors) = await PushPageAsync(
                (id, items) => _executionService.SyncInfrastructureAsync(2, id, items, syncDocuments: false), historyId, pageItems, $"Đường dây skip={skip}");
            success += pageSuccess;
            failed += pageFailed;
            warnings += pageWarnings;
            errors.AddRange(pageErrors);

            if (result.Items.Count < pageSize || skip + pageItems.Count >= result.Total)
            {
                await _syncConfigRepository.UpdateSyncCursorAsync(SyncObjectType.TransmissionLine, null);
                return await FinishSubstationOrLineAsync(SyncObjectType.TransmissionLine, 2, config, historyId, total, success, failed, warnings, errors);
            }
            skip += pageSize;

            if (HasHitSafetyCap(total, "Đường dây", ref warnings))
            {
                await _syncConfigRepository.UpdateSyncCursorAsync(SyncObjectType.TransmissionLine, skip.ToString());
                return await FinishSubstationOrLineAsync(SyncObjectType.TransmissionLine, 2, config, historyId, total, success, failed, warnings, errors);
            }

            // Cha (PARENT_ID) của mỗi Đường dây được tra TRỰC TIẾP theo mã PMIS "maCha" ngay trong lượt sync
            // này (xem InfrastructureRepository.UpsertFromPmisAsync) — không cần job nền riêng nào để "khớp
            // lại" nữa; chỉ "lỡ nhịp" khi trục CHƯA từng được đồng bộ tới, và tự khớp đúng ở lượt kế tiếp
            // (PMIS trả toàn bộ dữ liệu mỗi lượt, không phải delta).
        }
    }

    /// <summary>Gọi ở MỌI điểm thoát của RunSubstationAsync/RunLineAsync (kể cả lỗi gọi PMIS, hoàn tất
    /// trọn vẹn, chạm trần an toàn) — chạy pass đồng bộ tài liệu RIÊNG có rotation (xem
    /// SyncDocumentsRotatingAsync) rồi gộp warnings/errors vào kết quả cuối cùng của lượt. Chạy độc lập với
    /// kết quả phân trang chính ở trên (kể cả khi lấy danh sách PMIS thất bại ngay từ đầu, vẫn còn owner đã
    /// đồng bộ TỪ TRƯỚC trong DB để tiếp tục xoay vòng đồng bộ tài liệu cho họ).</summary>
    private async Task<(int Total, int Success, int Failed, int Warnings, List<string> Errors)> FinishSubstationOrLineAsync(
        string objectType, int infraTypeId, SyncConfig config, string historyId,
        int total, int success, int failed, int warnings, List<string> errors)
    {
        var (docWarnings, docErrors) = await SyncDocumentsRotatingAsync(objectType, infraTypeId, historyId, config.DocumentSyncCursor);
        warnings += docWarnings;
        errors.AddRange(docErrors);
        return (total, success, failed, warnings, errors);
    }

    /// <summary>Pass RIÊNG (chạy SAU vòng lặp phân trang chính của RunSubstationAsync/RunLineAsync) đồng bộ
    /// tài liệu đính kèm/ảnh QR cho Trạm/Đường dây, có ROTATION qua SyncConfig.DocumentSyncCursor — giống
    /// hệt cách RunEquipmentAsync xoay vòng ưu tiên ngân sách ChiTietThietBi/QR qua SyncCursor. Danh sách
    /// owner lấy từ CHÍNH DB mình (đã đồng bộ từ trước, rẻ — không phải live PMIS pagination), nên xoay
    /// vòng được TRƯỚC khi lặp, khác hẳn cách dữ liệu chính (Trạm/Đường dây) phải fetch trực tiếp từ PMIS
    /// theo trang. Xem Migration0013_AddDocumentSyncCursorToSyncConfig để biết lý do cần cursor riêng
    /// (không dùng chung SyncCursor — 2 mục đích khác nhau: SyncCursor = vị trí phân trang dữ liệu chính,
    /// DocumentSyncCursor = owner nào được ưu tiên đồng bộ tài liệu ở lượt này).</summary>
    private async Task<(int Warnings, List<string> Errors)> SyncDocumentsRotatingAsync(
        string objectType, int infraTypeId, string historyId, string? cursor)
    {
        List<SyncedInfrastructurePmisCode> owners;
        try
        {
            var all = await _equipmentServiceClient.GetSyncedInfrastructurePmisCodesAsync();
            owners = RotateParentsByCursor(all.Where(o => o.InfraTypeId == infraTypeId).ToList(), cursor);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisScheduledSyncJob: lỗi khi lấy danh sách owner để đồng bộ tài liệu ({ObjectType}).", objectType);
            return (0, [$"Đồng bộ tài liệu {objectType}: {SyncErrorFormatter.FormatShort(ex)}"]);
        }

        var warnings = 0;
        var allDetails = new List<SyncHistoryDetail>();
        string? cursorForNextRun = null;
        foreach (var owner in owners)
        {
            var (w, details) = await _executionService.SyncDocumentsForInfrastructureOwnerAsync(owner.PmisCode, infraTypeId, historyId);
            warnings += w;
            allDetails.AddRange(details);

            if (_executionService.DocumentSyncBudgetExhausted)
            {
                cursorForNextRun = owner.PmisCode;
                break;
            }
        }
        if (allDetails.Count > 0) await _syncHistoryRepository.InsertDetailsAsync(allDetails);
        // null nếu ngân sách KHÔNG BAO GIỜ cạn (mọi owner đều được đồng bộ tài liệu) — xoá cursor, lượt sau
        // bắt đầu lại từ đầu danh sách.
        await _syncConfigRepository.UpdateDocumentSyncCursorAsync(objectType, cursorForNextRun);
        return (warnings, []);
    }

    private async Task<(int Total, int Success, int Failed, int Warnings, List<string> Errors)> RunEquipmentAsync(SyncConfig config, string historyId)
    {
        // Thiết bị không có API "lấy tất cả" — phải lặp theo từng Trạm/Đường dây đã đồng bộ trước đó
        // (module 3) để lấy thiết bị con, đúng theo 2 API riêng biệt của tài liệu PMIS.
        // Xoay vòng theo SyncConfig.SyncCursor (mã PMIS cha nơi lượt trước dùng hết ngân sách gọi PMIS
        // thật ChiTietThietBi/QR — xem PmisSyncExecutionService.EquipmentDetailBudgetExhausted) để mỗi lượt
        // ưu tiên ngân sách cho 1 nhóm cha KHÁC nhau, tránh các cha cuối danh sách không bao giờ được enrich
        // (GetSyncedInfrastructurePmisCodesAsync giờ đã ORDER BY ổn định, cần thiết để xoay vòng có ý nghĩa).
        var parents = RotateParentsByCursor(await _equipmentServiceClient.GetSyncedInfrastructurePmisCodesAsync(), config.SyncCursor);
        var substationDevicePageSize = await GetPageSizeAsync("SUBSTATION_DEVICE_LIST");
        var lineDevicePageSize = await GetPageSizeAsync("LINE_DEVICE_LIST");
        int total = 0, success = 0, failed = 0, warnings = 0;
        var errors = new List<string>();
        string? budgetExhaustedAtParent = null;
        string? safetyCapAtParent = null;
        var parentsVisited = 0;

        foreach (var parent in parents)
        {
            parentsVisited++;
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

            // Ghi nhận cha ĐẦU TIÊN (theo thứ tự đã xoay vòng) mà ngân sách gọi PMIS thật cạn ngay sau khi
            // xử lý xong — mọi cha SAU nó trong lượt này chắc chắn không còn ngân sách để enrich
            // ThongSoKyThuat/QR. Lượt sau sẽ xoay vòng bắt đầu NGAY SAU cha này, ưu tiên đúng nhóm bị bỏ lỡ.
            if (budgetExhaustedAtParent == null && _executionService.EquipmentDetailBudgetExhausted)
                budgetExhaustedAtParent = parent.PmisCode;

            if (parentsVisited >= MaxParentsPerRunEquipment)
            {
                Log.Warning("PmisScheduledSyncJob: Thiết bị đã thăm {Visited} cha (giới hạn an toàn {Max}), dừng lại dù danh sách cha có thể còn nữa — sẽ tiếp tục ở lượt sau.", parentsVisited, MaxParentsPerRunEquipment);
                warnings++;
                safetyCapAtParent = parent.PmisCode;
                break;
            }
        }

        // Ưu tiên cursor theo trần an toàn số-cha (nếu chạm — nghĩa là còn cha CHƯA thăm tới, quan trọng
        // hơn để không bỏ sót) hơn cursor theo ngân sách enrich (nếu chỉ ngân sách cạn nhưng đã thăm hết
        // mọi cha, cursor ngân sách vẫn đúng ý nghĩa "ưu tiên nhóm bị bỏ lỡ enrich" cho lượt sau). null nếu
        // CẢ HAI đều không xảy ra trong lượt này (thăm hết cha, không cạn ngân sách) — xoá cursor, lượt sau
        // bắt đầu lại từ đầu danh sách (thứ tự gốc, không cần ưu tiên ai).
        await _syncConfigRepository.UpdateSyncCursorAsync(SyncObjectType.Equipment, safetyCapAtParent ?? budgetExhaustedAtParent);

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
