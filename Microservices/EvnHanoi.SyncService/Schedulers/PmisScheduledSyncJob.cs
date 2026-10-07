using System.Diagnostics;
using System.Text.Json;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Infrastructure.Messaging;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Models.Pmis;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Services;
using Microsoft.Extensions.Options;
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
    // 7+ giờ). 4000 cha × ước lượng &lt;1s/cha (network round-trip PMIS thật) ≈ dưới 60-70 phút. Đây là trần
    // theo SỐ CHA; thời gian chạy tối đa thật của lượt do ngân sách = tần suất − đệm quyết định (RunBudgetClock).
    private const int MaxParentsPerRunEquipment = 4000;

    private readonly ISyncConfigRepository _syncConfigRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IPmisClient _pmisClient;
    private readonly IEquipmentServiceClient _equipmentServiceClient;
    private readonly IPmisSyncExecutionService _executionService;
    private readonly IDistributedLockFactory _lockFactory;
    private readonly IMessageProducer _messageProducer;
    private readonly IPmisEndpointConfigProvider _endpointConfigProvider;
    private readonly IPmisSyncStateRepository _stateRepository;
    private readonly IOptions<PmisIncrementalOptions> _incrementalOptions;
    private readonly IOptions<SyncScheduleOptions> _scheduleOptions;

    /// <summary>Đồng hồ ngân sách thời gian của lượt này (tần suất cấu hình − đệm, xem <see cref="SyncRunBudget"/>) — tạo trong
    /// RunIfDueAsync sau khi giành được khoá. Quartz tạo 1 instance job mới cho mỗi lần chạy nên lưu ở field là an toàn.</summary>
    private RunBudgetClock _budget = new(TimeSpan.MaxValue);

    /// <summary>true nếu lượt này dừng sớm vì hết ngân sách thời gian (không phải lỗi, lượt sau tiếp tục theo cursor/LAST_SCAN_AT).</summary>
    private bool _stoppedByTimeBudget;

    private bool BudgetExceeded()
    {
        if (!_budget.Exceeded) return false;
        if (!_stoppedByTimeBudget)
        {
            _stoppedByTimeBudget = true;
            Log.Information("PmisScheduledSyncJob: hết ngân sách thời gian {Minutes:0} phút (tần suất − đệm) — dừng mềm, lượt sau tiếp tục.", _budget.Budget.TotalMinutes);
        }
        return true;
    }

    /// <summary>true nếu lượt Thiết bị này không có cha nào đến hạn quét (đồng bộ tăng dần đang rảnh) — không phải
    /// bất thường nên không cảnh báo "0 bản ghi" (xem RunIfDueAsync). Quartz tạo 1 instance mới cho mỗi lần chạy.</summary>
    private bool _incrementalIdle;

    // Thống kê đồng bộ tăng dần của lượt này (chỉ để log kiểm chứng hiệu quả, xem RunIfDueAsync).
    private int _incrementalUnchanged;
    private int _incrementalPushed;

    public PmisScheduledSyncJob(
        ISyncConfigRepository syncConfigRepository,
        ISyncHistoryRepository syncHistoryRepository,
        IPmisClient pmisClient,
        IEquipmentServiceClient equipmentServiceClient,
        IPmisSyncExecutionService executionService,
        IDistributedLockFactory lockFactory,
        IMessageProducer messageProducer,
        IPmisEndpointConfigProvider endpointConfigProvider,
        IPmisSyncStateRepository stateRepository,
        IOptions<PmisIncrementalOptions> incrementalOptions,
        IOptions<SyncScheduleOptions> scheduleOptions)
    {
        _syncConfigRepository = syncConfigRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _pmisClient = pmisClient;
        _equipmentServiceClient = equipmentServiceClient;
        _executionService = executionService;
        _lockFactory = lockFactory;
        _messageProducer = messageProducer;
        _endpointConfigProvider = endpointConfigProvider;
        _stateRepository = stateRepository;
        _incrementalOptions = incrementalOptions;
        _scheduleOptions = scheduleOptions;
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

    /// <summary>Mốc bắt đầu của đợt quét đầy đủ đang diễn ra (UTC), hoặc null nếu chưa đến hạn quét đầy đủ / tắt tăng
    /// dần. Đến hạn = chưa từng quét xong, hoặc lần quét xong gần nhất đã quá <see cref="PmisIncrementalOptions.FullSweepIntervalHours"/>.
    /// Mốc được lưu (dòng SWEEP "{objectType}_START") ngay lần đầu đến hạn và GIỮ NGUYÊN cho tới khi đợt quét xong,
    /// nên đợt quét bị cắt giữa chừng vẫn tiếp tục được ở lượt sau (xem <see cref="IncrementalContext.SweepStartUtc"/>).</summary>
    private async Task<DateTime?> GetSweepStartAsync(string objectType)
    {
        var opt = _incrementalOptions.Value;
        if (!opt.Enabled) return null;

        var lastDone = await _stateRepository.GetSweepAtAsync(objectType);
        if (lastDone != null && DateTime.UtcNow - lastDone.Value < TimeSpan.FromHours(opt.FullSweepIntervalHours))
            return null;

        var startKey = objectType + "_START";
        var start = await _stateRepository.GetSweepAtAsync(startKey);
        if (start != null && (lastDone == null || start.Value > lastDone.Value)) return start; // đợt quét đang dở
        await _stateRepository.SetSweepAtAsync(startKey);
        return await _stateRepository.GetSweepAtAsync(startKey);
    }

    /// <summary>Đẩy 1 trang Trạm/Đường dây. Đồng bộ tăng dần tắt → đúng hành vi cũ (PushPageAsync). Bật → bỏ qua
    /// bản ghi không đổi và ghi PMIS_SYNC_STATE CHỈ cho mã đã đẩy thành công. Lỗi ghi trạng thái chỉ log cảnh
    /// báo (lượt sau đẩy lại — an toàn). Bản ghi không đổi tính vào Success.</summary>
    private async Task<(int Success, int Failed, int Warnings, List<string> Errors)> PushInfrastructurePageAsync(
        string objectType, int infraTypeId, string historyId, List<JsonElement> pageItems, string pageLabel, DateTime? sweepStart)
    {
        var opt = _incrementalOptions.Value;
        if (!opt.Enabled)
        {
            return await PushPageAsync(
                (id, items) => _executionService.SyncInfrastructureAsync(infraTypeId, id, items, syncDocuments: false),
                historyId, pageItems, pageLabel);
        }

        var codes = pageItems.Select(i => PmisRecordHasher.InfrastructureCode(i, infraTypeId)).Where(c => c.Length > 0).ToList();
        var inc = new IncrementalContext
        {
            Existing = await _stateRepository.GetAsync(objectType, codes),
            SweepStartUtc = sweepStart,
            HashVersion = opt.HashVersion
        };

        var (success, failed, warnings, errors) = await PushPageAsync(
            (id, items) => _executionService.SyncInfrastructureAsync(infraTypeId, id, items, syncDocuments: false, inc),
            historyId, pageItems, pageLabel);

        await SaveIncrementalStateAsync(objectType, inc);
        _incrementalUnchanged += inc.UnchangedCodes.Count;
        _incrementalPushed += inc.ToSave.Count;
        return (success + inc.UnchangedCodes.Count, FailedExcludingUnchanged(failed, pageItems.Count, inc), warnings, errors);
    }

    /// <summary>PushPageAsync bắt exception của cả trang và trả failed = số bản ghi của trang (kể cả bản ghi không
    /// đổi vốn đã bị bỏ qua hợp lệ) — trừ chúng ra để không đếm 1 bản ghi vừa là Success vừa là Failed.</summary>
    private static int FailedExcludingUnchanged(int failed, int pageCount, IncrementalContext inc) =>
        inc.UnchangedCodes.Count > 0 && failed >= pageCount ? failed - inc.UnchangedCodes.Count : failed;

    private async Task SaveIncrementalStateAsync(string objectType, IncrementalContext inc)
    {
        try
        {
            if (inc.ToSave.Count > 0) await _stateRepository.UpsertPushedAsync(objectType, inc.ToSave, inc.HashVersion);
            if (inc.UnchangedCodes.Count > 0) await _stateRepository.TouchSeenAsync(objectType, inc.UnchangedCodes);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PmisScheduledSyncJob: lỗi ghi PMIS_SYNC_STATE ({ObjectType}), lượt sau sẽ đẩy lại các bản ghi này.", objectType);
        }
    }

    /// <summary>Chọn và sắp xếp Trạm/Đường dây cha cần quét thiết bị con ở chế độ tăng dần. Ưu tiên: 0 = chưa từng
    /// quét; 1 = cha vừa được đẩy (mới/đổi) SAU lần quét gần nhất; 2 = đến hạn quét lại (hoặc chưa quét kể từ mốc bắt đầu đợt quét đầy đủ);
    /// cha còn "mới" (đã quét trong khoảng <paramref name="rescanInterval"/> và — khi đang quét đầy đủ — sau mốc
    /// <paramref name="sweepStartUtc"/>) bị loại. Trong cùng mức ưu tiên:
    /// LAST_SCAN_AT cũ nhất trước, rồi theo mã PMIS để thứ tự ổn định.</summary>
    internal static List<SyncedInfrastructurePmisCode> OrderParentsForScan(
        List<SyncedInfrastructurePmisCode> all,
        IReadOnlyDictionary<string, PmisSyncStateRow> scanState,
        IReadOnlyDictionary<string, PmisSyncStateRow> substationState,
        IReadOnlyDictionary<string, PmisSyncStateRow> lineState,
        DateTime? sweepStartUtc, TimeSpan rescanInterval, DateTime nowUtc)
    {
        (int Priority, DateTime LastScan) Rank(SyncedInfrastructurePmisCode p)
        {
            scanState.TryGetValue(p.PmisCode, out var scan);
            if (scan?.LastScanAt == null) return (0, DateTime.MinValue);

            var infraState = p.InfraTypeId == 1 ? substationState : lineState;
            if (infraState.TryGetValue(p.PmisCode, out var infra) && infra.LastPushedAt > scan.LastScanAt)
                return (1, scan.LastScanAt.Value);

            if ((sweepStartUtc != null && scan.LastScanAt < sweepStartUtc) || nowUtc - scan.LastScanAt.Value >= rescanInterval)
                return (2, scan.LastScanAt.Value);

            return (3, scan.LastScanAt.Value);
        }

        return all
            .Select(p => (Parent: p, Rank: Rank(p)))
            .Where(x => x.Rank.Priority < 3)
            .OrderBy(x => x.Rank.Priority)
            .ThenBy(x => x.Rank.LastScan)
            .ThenBy(x => x.Parent.PmisCode, StringComparer.Ordinal)
            .Select(x => x.Parent)
            .ToList();
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

        // Thời gian chạy tối đa = tần suất (không thấp hơn mức tối thiểu) − đệm — thay cho ngưỡng 60/35 phút cố định.
        var effectiveFrequency = SyncRunBudget.EffectiveFrequency(config.FrequencyValue, config.FrequencyUnit, _scheduleOptions.Value);
        _budget = new RunBudgetClock(SyncRunBudget.For(effectiveFrequency, _scheduleOptions.Value));
        if (_budget.Budget < TimeSpan.FromMinutes(20))
            Log.Warning("PmisScheduledSyncJob: ngân sách thời gian của {ObjectType} chỉ {Minutes:0} phút (tần suất {Frequency}) — mỗi lượt làm được rất ít việc.", objectType, _budget.Budget.TotalMinutes, effectiveFrequency);

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
            var zeroRecordsMessage = total == 0 && status == SyncHistoryStatus.Success && !_incrementalIdle
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
                Log.Warning("PmisScheduledSyncJob: {ObjectType} hoàn tất với kết quả thật ({Status}, total={Total}, success={Success}) nhưng syncHistoryId={SyncHistoryId} đã bị SyncHistoryWatchdogJob đánh FAILED trước đó (chạy quá ngưỡng an toàn, xem SyncRunBudget.StaleAfter) — giữ nguyên FAILED của watchdog, bỏ kết quả thật này.", objectType, status, total, success, historyId);

            if (_incrementalOptions.Value.Enabled)
            {
                Log.Information("PmisScheduledSyncJob: {ObjectType} đồng bộ tăng dần — PMIS trả {Total} bản ghi, đã đẩy {Pushed}, bỏ qua {Unchanged} bản ghi không đổi, lỗi {Failed}.",
                    objectType, total, _incrementalPushed, _incrementalUnchanged, failed);
            }

            // Lượt chạy hoàn tất bình thường (kể cả Failed do 0/n item thành công vẫn là 1 lượt đã thử
            // xong) — đẩy NextSyncAt theo tần suất cấu hình, và reset bộ đếm lỗi liên tiếp vì PMIS đã
            // phản hồi được (dù dữ liệu bên trong có lỗi riêng lẻ hay không).
            var nextSyncAt = now.Add(effectiveFrequency);
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
            var nextSyncAtOnFailure = now.Add(effectiveFrequency);
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
        var sweepStart = await GetSweepStartAsync(SyncObjectType.Substation);
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

            var (pageSuccess, pageFailed, pageWarnings, pageErrors) = await PushInfrastructurePageAsync(
                SyncObjectType.Substation, 1, historyId, pageItems, $"Trạm biến áp skip={skip}", sweepStart);
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
                await MarkSweepDoneAsync(SyncObjectType.Substation, sweepStart, failed, errors);
                return await FinishSubstationOrLineAsync(SyncObjectType.Substation, 1, config, historyId, total, success, failed, warnings, errors);
            }
            skip += pageSize;

            if (HasHitSafetyCap(total, "Trạm biến áp", ref warnings) || BudgetExceeded())
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
        var sweepStart = await GetSweepStartAsync(SyncObjectType.TransmissionLine);
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

            var (pageSuccess, pageFailed, pageWarnings, pageErrors) = await PushInfrastructurePageAsync(
                SyncObjectType.TransmissionLine, 2, historyId, pageItems, $"Đường dây skip={skip}", sweepStart);
            success += pageSuccess;
            failed += pageFailed;
            warnings += pageWarnings;
            errors.AddRange(pageErrors);

            if (result.Items.Count < pageSize || skip + pageItems.Count >= result.Total)
            {
                await _syncConfigRepository.UpdateSyncCursorAsync(SyncObjectType.TransmissionLine, null);
                await MarkSweepDoneAsync(SyncObjectType.TransmissionLine, sweepStart, failed, errors);
                return await FinishSubstationOrLineAsync(SyncObjectType.TransmissionLine, 2, config, historyId, total, success, failed, warnings, errors);
            }
            skip += pageSize;

            if (HasHitSafetyCap(total, "Đường dây", ref warnings) || BudgetExceeded())
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

    /// <summary>Ghi mốc "quét đầy đủ xong" khi lượt đầy đủ này duyệt hết danh sách PMIS mà không có lỗi — lượt
    /// sau mới lại dùng hash để bỏ qua bản ghi không đổi. Chỉ có tác dụng khi bật đồng bộ tăng dần.</summary>
    private async Task MarkSweepDoneAsync(string objectType, DateTime? sweepStart, int failed, List<string> errors)
    {
        if (!_incrementalOptions.Value.Enabled || sweepStart == null || failed > 0 || errors.Count > 0) return;
        try { await _stateRepository.SetSweepAtAsync(objectType); }
        catch (Exception ex) { Log.Warning(ex, "PmisScheduledSyncJob: lỗi ghi mốc quét đầy đủ ({ObjectType}).", objectType); }
    }

    /// <summary>Gọi ở MỌI điểm thoát của RunSubstationAsync/RunLineAsync. TRƯỚC ĐÂY chạy thêm 1 pass đồng bộ tài liệu đính kèm lồng trong
    /// lượt này (SyncDocumentsRotatingAsync) — nhưng pass đó kéo lại danh sách tài liệu (kèm base64 file) của mọi owner mỗi chu kỳ,
    /// chiếm hết thời gian và làm lượt bị đánh FAILED. Tài liệu giờ do job DOCUMENT riêng đảm nhiệm
    /// (<see cref="PmisDocumentListSyncJob"/>: đếm trước, chỉ lấy phần mới theo ngày, ngân sách thời gian riêng).</summary>
    private static Task<(int Total, int Success, int Failed, int Warnings, List<string> Errors)> FinishSubstationOrLineAsync(
        string objectType, int infraTypeId, SyncConfig config, string historyId,
        int total, int success, int failed, int warnings, List<string> errors) =>
        Task.FromResult((total, success, failed, warnings, errors));

    private async Task<(int Total, int Success, int Failed, int Warnings, List<string> Errors)> RunEquipmentAsync(SyncConfig config, string historyId)
    {
        // Thiết bị không có API "lấy tất cả" — phải lặp theo từng Trạm/Đường dây đã đồng bộ trước đó
        // (module 3) để lấy thiết bị con, đúng theo 2 API riêng biệt của tài liệu PMIS.
        // Xoay vòng theo SyncConfig.SyncCursor (mã PMIS cha nơi lượt trước dùng hết ngân sách gọi PMIS
        // thật ChiTietThietBi/QR — xem PmisSyncExecutionService.EquipmentDetailBudgetExhausted) để mỗi lượt
        // ưu tiên ngân sách cho 1 nhóm cha KHÁC nhau, tránh các cha cuối danh sách không bao giờ được enrich
        // (GetSyncedInfrastructurePmisCodesAsync giờ đã ORDER BY ổn định, cần thiết để xoay vòng có ý nghĩa).
        var opt = _incrementalOptions.Value;
        var allParents = await _equipmentServiceClient.GetSyncedInfrastructurePmisCodesAsync();
        var sweepStart = await GetSweepStartAsync(SyncObjectType.Equipment);
        List<SyncedInfrastructurePmisCode> parents;
        if (opt.Enabled)
        {
            var parentScanState = await _stateRepository.GetAllAsync("PARENT_SCAN");
            // Đợt quét đầy đủ xong khi mọi cha đã được quét kể từ mốc bắt đầu → ghi mốc hoàn tất và quay về chế độ thường.
            if (sweepStart != null && !allParents.Any(p => !parentScanState.TryGetValue(p.PmisCode, out var sc) || sc.LastScanAt == null || sc.LastScanAt < sweepStart))
            {
                try { await _stateRepository.SetSweepAtAsync(SyncObjectType.Equipment); }
                catch (Exception ex) { Log.Warning(ex, "PmisScheduledSyncJob: lỗi ghi mốc quét đầy đủ (EQUIPMENT)."); }
                sweepStart = null;
            }

            // Đồng bộ tăng dần: chỉ quét cha CHƯA quét / vừa mới-đổi / đến hạn quét lại, ưu tiên theo LAST_SCAN_AT
            // cũ nhất (thay cho xoay vòng theo SyncCursor) — xem OrderParentsForScan.
            parents = OrderParentsForScan(
                allParents,
                parentScanState,
                await _stateRepository.GetAllAsync(SyncObjectType.Substation),
                await _stateRepository.GetAllAsync(SyncObjectType.TransmissionLine),
                sweepStart, TimeSpan.FromHours(opt.ParentRescanIntervalHours), DateTime.UtcNow);
            if (parents.Count == 0)
            {
                // Không có cha nào đến hạn quét — bình thường ở chế độ tăng dần, không phải bất thường (xem RunIfDueAsync).
                _incrementalIdle = true;
                await _syncConfigRepository.UpdateSyncCursorAsync(SyncObjectType.Equipment, null);
                return (0, 0, 0, 0, []);
            }
        }
        else
        {
            parents = RotateParentsByCursor(allParents, config.SyncCursor);
        }
        var substationDevicePageSize = await GetPageSizeAsync("SUBSTATION_DEVICE_LIST");
        var lineDevicePageSize = await GetPageSizeAsync("LINE_DEVICE_LIST");
        int total = 0, success = 0, failed = 0, warnings = 0;
        var errors = new List<string>();
        string? budgetExhaustedAtParent = null;
        string? safetyCapAtParent = null;
        var parentsVisited = 0;
        // Chế độ xoay vòng (không gia tăng): mã cha đã quét TRỌN VẸN gần nhất — nếu hết ngân sách thời gian giữa chừng
        // 1 cha thì cursor trỏ về cha này để lượt sau bắt đầu lại đúng cha dở (RotateParentsByCursor bắt đầu SAU cursor).
        string? lastCompletedParent = null;
        string? timeBudgetCursor = null;

        foreach (var parent in parents)
        {
            parentsVisited++;
            var parentOk = true; // false nếu lấy danh sách PMIS hoặc lưu thiết bị của cha này có lỗi → chưa coi là đã quét
            var pageSize = parent.InfraTypeId == 1 ? substationDevicePageSize : lineDevicePageSize;
            var skip = 0;
            while (true)
            {
                // Hết ngân sách thời gian giữa chừng 1 cha lớn (nhiều trang): dừng trước khi gọi PMIS trang kế, cha này chưa
                // coi là quét xong (parentOk=false) nên lượt sau quét lại.
                if (BudgetExceeded())
                {
                    parentOk = false;
                    break;
                }

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
                    parentOk = false;
                    break;
                }

                total += pageItems.Count;
                IncrementalContext? inc = null;
                if (opt.Enabled)
                {
                    var codes = pageItems.Select(PmisRecordHasher.EquipmentCode).Where(c => c.Length > 0).ToList();
                    inc = new IncrementalContext
                    {
                        Existing = await _stateRepository.GetAsync(SyncObjectType.Equipment, codes),
                        SweepStartUtc = sweepStart,
                        HashVersion = opt.HashVersion
                    };
                }
                var (pageSuccess, pageFailed, pageWarnings, pageErrors) = await PushPageAsync(
                    (hId, items) => _executionService.SyncEquipmentAsync(hId, items, parent.PmisCode, inc, syncDocuments: false),
                    historyId, pageItems, $"Thiết bị cha={parent.PmisCode} skip={skip}");
                if (inc != null)
                {
                    await SaveIncrementalStateAsync(SyncObjectType.Equipment, inc);
                    _incrementalUnchanged += inc.UnchangedCodes.Count;
                    _incrementalPushed += inc.ToSave.Count;
                    pageSuccess += inc.UnchangedCodes.Count; // không đổi tính là thành công
                    pageFailed = FailedExcludingUnchanged(pageFailed, pageItems.Count, inc);
                }
                if (pageFailed > 0) parentOk = false;
                success += pageSuccess;
                failed += pageFailed;
                warnings += pageWarnings;
                errors.AddRange(pageErrors);

                if (pageCount < pageSize || pageCount == 0) break;
                skip += pageSize;

                if (HasHitSafetyCap(skip, $"Thiết bị cha={parent.PmisCode}", ref warnings))
                {
                    parentOk = false; // cha có quá nhiều trang, mới quét dở → chưa coi là đã quét xong
                    break;
                }
            }

            // Ghi nhận cha ĐẦU TIÊN (theo thứ tự đã xoay vòng) mà ngân sách gọi PMIS thật cạn ngay sau khi
            // xử lý xong — mọi cha SAU nó trong lượt này chắc chắn không còn ngân sách để enrich
            // ThongSoKyThuat/QR. Lượt sau sẽ xoay vòng bắt đầu NGAY SAU cha này, ưu tiên đúng nhóm bị bỏ lỡ.
            if (budgetExhaustedAtParent == null && _executionService.EquipmentDetailBudgetExhausted)
                budgetExhaustedAtParent = parent.PmisCode;

            // Đồng bộ tăng dần: ghi mốc quét cha CHỈ khi cha này quét trọn vẹn không lỗi VÀ ngân sách enrich chưa
            // cạn (cạn nghĩa là có thiết bị TBA chưa lấy đủ chi tiết → cha phải được quét lại sớm ở lượt sau).
            if (opt.Enabled && parentOk && !_executionService.EquipmentDetailBudgetExhausted)
            {
                try { await _stateRepository.MarkParentScannedAsync(parent.PmisCode); }
                catch (Exception ex) { Log.Warning(ex, "PmisScheduledSyncJob: lỗi ghi mốc quét cha {PmisCode}.", parent.PmisCode); }
            }

            if (parentOk) lastCompletedParent = parent.PmisCode;

            if (BudgetExceeded())
            {
                // Hành vi bình thường (không tăng warnings): lượt sau tiếp tục — chế độ tăng dần theo LAST_SCAN_AT cũ nhất,
                // chế độ xoay vòng theo cursor. Ngân sách = tần suất − đệm (SyncRunBudget).
                Log.Information("PmisScheduledSyncJob: Thiết bị đã quét {Visited}/{Total} cha trong {Minutes:0} phút (ngân sách thời gian), dừng — lượt sau tiếp tục.",
                    parentsVisited, parents.Count, _budget.Elapsed.TotalMinutes);
                timeBudgetCursor = lastCompletedParent ?? config.SyncCursor;
                break;
            }

            if (parentsVisited >= MaxParentsPerRunEquipment)
            {
                Log.Warning("PmisScheduledSyncJob: Thiết bị đã thăm {Visited} cha (giới hạn an toàn {Max}), dừng lại dù danh sách cha có thể còn nữa — sẽ tiếp tục ở lượt sau.", parentsVisited, MaxParentsPerRunEquipment);
                // Chế độ tăng dần: dừng ở trần số cha là bình thường (lượt sau tiếp tục theo LAST_SCAN_AT), không phải cảnh báo.
                if (!opt.Enabled) warnings++;
                safetyCapAtParent = parent.PmisCode;
                break;
            }
        }

        // Ưu tiên cursor theo trần an toàn số-cha (nếu chạm — nghĩa là còn cha CHƯA thăm tới, quan trọng
        // hơn để không bỏ sót) hơn cursor theo ngân sách enrich (nếu chỉ ngân sách cạn nhưng đã thăm hết
        // mọi cha, cursor ngân sách vẫn đúng ý nghĩa "ưu tiên nhóm bị bỏ lỡ enrich" cho lượt sau). null nếu
        // CẢ HAI đều không xảy ra trong lượt này (thăm hết cha, không cạn ngân sách) — xoá cursor, lượt sau
        // bắt đầu lại từ đầu danh sách (thứ tự gốc, không cần ưu tiên ai).
        if (opt.Enabled)
        {
            // Chế độ tăng dần không dùng cursor xoay vòng (thứ tự quét theo LAST_SCAN_AT) — xoá cursor cũ.
            await _syncConfigRepository.UpdateSyncCursorAsync(SyncObjectType.Equipment, null);
        }
        else
        {
            await _syncConfigRepository.UpdateSyncCursorAsync(SyncObjectType.Equipment, safetyCapAtParent ?? timeBudgetCursor ?? budgetExhaustedAtParent);
        }

        return (total, success, failed, warnings, errors);
    }
}
