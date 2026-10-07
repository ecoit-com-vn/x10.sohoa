using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Models.Internal;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Services;
using Microsoft.Extensions.Options;
using Quartz;
using RedLockNet;
using Serilog;

namespace EvnHanoi.SyncService.Schedulers;

/// <summary>
/// Đồng bộ DANH SÁCH (metadata) tài liệu đính kèm PMIS (API 8/9) — job RIÊNG, lịch riêng (SYNC_CONFIG 'DOCUMENT'), tách khỏi lượt
/// Trạm/Đường dây/Thiết bị. Lý do tách và cách làm:
///  - API danh sách của PMIS kèm cả file (base64, tới ~47 MB/tài liệu): kéo lại mọi owner mỗi chu kỳ ≈ hàng trăm GB, làm lượt chính
///    chạy quá giờ và bị đánh FAILED. File vật lý do PmisDocumentFileDownloadJob tải riêng qua TaiFileTaiLieu.
///  - Mỗi owner chỉ GỌI ĐẾM trước (skip vượt tổng → phản hồi ~50 byte): tổng không đổi thì bỏ qua; tổng tăng thì chỉ lấy phần mới theo
///    tuNgay; owner chưa từng đồng bộ mà DB đã đủ tài liệu thì chỉ ghi trạng thái; owner &gt; 50.000 tài liệu chia theo khoảng ngày.
///  - Thời gian chạy tối đa của lượt = tần suất − đệm (SyncRunBudget); hết giờ dừng mềm, trạng thái từng owner ghi ở PMIS_SYNC_STATE
///    nên lượt sau tiếp tục đúng chỗ.
/// RedLock theo tên job + [DisallowConcurrentExecution] chặn chạy chồng.
/// </summary>
[DisallowConcurrentExecution]
public class PmisDocumentListSyncJob : IJob
{
    private const string OwnerStateType = "DOC_OWNER";
    private const string WindowStateType = "DOC_WINDOW";
    private const int FailureNotifyThreshold = 5;

    private readonly ISyncConfigRepository _syncConfigRepository;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly IPmisClient _pmisClient;
    private readonly IEquipmentServiceClient _equipmentServiceClient;
    private readonly IPmisSyncExecutionService _executionService;
    private readonly IPmisSyncStateRepository _stateRepository;
    private readonly IPmisEndpointConfigProvider _endpointConfigProvider;
    private readonly IDistributedLockFactory _lockFactory;
    private readonly IOptions<PmisDocumentSyncOptions> _options;
    private readonly IOptions<SyncScheduleOptions> _scheduleOptions;

    public PmisDocumentListSyncJob(
        ISyncConfigRepository syncConfigRepository,
        ISyncHistoryRepository syncHistoryRepository,
        IPmisClient pmisClient,
        IEquipmentServiceClient equipmentServiceClient,
        IPmisSyncExecutionService executionService,
        IPmisSyncStateRepository stateRepository,
        IPmisEndpointConfigProvider endpointConfigProvider,
        IDistributedLockFactory lockFactory,
        IOptions<PmisDocumentSyncOptions> options,
        IOptions<SyncScheduleOptions> scheduleOptions)
    {
        _syncConfigRepository = syncConfigRepository;
        _syncHistoryRepository = syncHistoryRepository;
        _pmisClient = pmisClient;
        _equipmentServiceClient = equipmentServiceClient;
        _executionService = executionService;
        _stateRepository = stateRepository;
        _endpointConfigProvider = endpointConfigProvider;
        _lockFactory = lockFactory;
        _options = options;
        _scheduleOptions = scheduleOptions;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await RunIfDueAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisDocumentListSyncJob: lỗi không mong đợi.");
        }
    }

    private sealed class RunStats
    {
        public int Skip, Seed, Delta, Full, Windowed, Errors, Checked;
        public int Total, Failed, Warnings, Created;
        public bool Aborted, Paused;
        public readonly List<string> Messages = [];
        public void AddMessage(string m) { if (Messages.Count < 5) Messages.Add(m.Length > 300 ? m[..300] : m); }
    }

    private async Task RunIfDueAsync()
    {
        var config = await _syncConfigRepository.GetByObjectTypeAsync(SyncObjectType.Document);
        if (config == null || !config.IsEnabled) return;

        var now = DateTime.UtcNow;
        if (config.NextSyncAt != null && config.NextSyncAt > now) return;

        var substationActive = await _endpointConfigProvider.GetEndpointAsync("SUBSTATION_DOCUMENT_LIST") != null;
        var lineActive = await _endpointConfigProvider.GetEndpointAsync("LINE_DOCUMENT_LIST") != null;
        if (!substationActive && !lineActive)
        {
            Log.Debug("PmisDocumentListSyncJob: bỏ qua vì cả 2 API danh sách tài liệu (8/9) chưa cấu hình hoặc đang tắt.");
            return;
        }

        await using var redLock = await _lockFactory.CreateLockAsync(
            "sync:lock:pmis:document-list", TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));
        if (!redLock.IsAcquired)
        {
            Log.Information("PmisDocumentListSyncJob: đang chạy ở tiến trình khác, bỏ qua lượt này.");
            return;
        }

        var effectiveFrequency = SyncRunBudget.EffectiveFrequency(config.FrequencyValue, config.FrequencyUnit, _scheduleOptions.Value);
        var budget = new RunBudgetClock(SyncRunBudget.For(effectiveFrequency, _scheduleOptions.Value));

        var historyId = await _syncHistoryRepository.CreateAsync(new SyncHistory
        {
            SyncConfigId = config.Id,
            ObjectType = SyncObjectType.Document,
            SyncType = SyncType.Auto,
            StartTime = now,
            Status = SyncHistoryStatus.Running,
            CreatedBy = "SYSTEM"
        });

        var stats = new RunStats();
        try
        {
            await RunAsync(historyId, budget, substationActive, lineActive, stats);

            var status = stats.Aborted ? SyncHistoryStatus.Failed
                : stats.Failed > 0 || stats.Warnings > 0 || stats.Errors > 0 || stats.Paused ? SyncHistoryStatus.Warning
                : SyncHistoryStatus.Success;
            var summary = $"Kiểm tra {stats.Checked} owner: bỏ qua {stats.Skip}, ghi trạng thái {stats.Seed}, lấy phần mới {stats.Delta}, quét đầy đủ {stats.Full}, theo khoảng ngày {stats.Windowed}; {stats.Created} tài liệu mới.";
            var error = stats.Messages.Count > 0 ? summary + " " + string.Join("; ", stats.Messages) : summary;
            if (error.Length > 1800) error = error[..1800];
            await _syncHistoryRepository.CompleteAsync(historyId, status, stats.Total, stats.Total - stats.Failed, stats.Failed, error);
            Log.Information("PmisDocumentListSyncJob: {Status} — {Summary} (đã chạy {Minutes:0.0}/{Budget:0} phút).", status, summary, budget.Elapsed.TotalMinutes, budget.Budget.TotalMinutes);

            // Tạm dừng vì circuit breaker PMIS: thử lại sớm (15 phút) thay vì chờ cả tần suất cấu hình.
            var nextRun = stats.Paused ? now.AddMinutes(15) : now.Add(effectiveFrequency);
            await _syncConfigRepository.UpdateRunResultAsync(SyncObjectType.Document, now, nextRun,
                stats.Aborted ? config.ConsecutiveFailureCount + 1 : 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisDocumentListSyncJob: lượt đồng bộ danh sách tài liệu thất bại.");
            await _syncHistoryRepository.CompleteAsync(historyId, SyncHistoryStatus.Failed, stats.Total, stats.Total - stats.Failed, stats.Failed, SyncErrorFormatter.Format(ex));
            await _syncConfigRepository.UpdateRunResultAsync(SyncObjectType.Document, now, now.Add(effectiveFrequency), config.ConsecutiveFailureCount + 1);
        }
    }

    private async Task RunAsync(string historyId, RunBudgetClock budget, bool substationActive, bool lineActive, RunStats stats)
    {
        var opt = _options.Value;
        var owners = (await _equipmentServiceClient.GetSyncedInfrastructurePmisCodesAsync())
            .Where(o => o.InfraTypeId == 1 ? substationActive : lineActive)
            .ToList();
        var states = await _stateRepository.GetDocumentStatesAsync(OwnerStateType);
        var now = DateTime.UtcNow;

        // Thứ tự ưu tiên: (0) đang quét dở → (1) chưa từng đồng bộ (bootstrap) → (2) tới hạn đếm lại, cũ nhất trước.
        var candidates = owners
            .Select(o => (Owner: o, State: states.GetValueOrDefault(o.PmisCode)))
            .Where(x => x.State == null || x.State.RemoteTotal == null || IsInProgress(x.State)
                        || DocumentOwnerSyncPlanner.IsCountDue(x.State, now, opt))
            .OrderBy(x => IsInProgress(x.State) ? 0 : x.State?.RemoteTotal == null ? 1 : 2)
            .ThenBy(x => x.State?.LastCountAt ?? DateTime.MinValue)
            .ToList();
        if (candidates.Count == 0)
        {
            Log.Information("PmisDocumentListSyncJob: không owner nào tới hạn kiểm tra (tổng {Owners} owner).", owners.Count);
            return;
        }

        // Số tài liệu đã có trong DB theo owner — chỉ nạp khi có owner cần bootstrap (1 câu GROUP BY).
        Dictionary<string, int>? dbCounts = null;
        var consecutiveErrors = 0;
        var allDetails = new List<SyncHistoryDetail>();

        foreach (var (owner, state) in candidates)
        {
            if (budget.Exceeded) break;
            if (consecutiveErrors >= opt.MaxConsecutiveOwnerErrors)
            {
                stats.Aborted = true;
                stats.AddMessage($"Dừng lượt: {consecutiveErrors} owner liên tiếp lỗi gọi PMIS.");
                Log.Error("PmisDocumentListSyncJob: dừng lượt vì {Count} owner liên tiếp lỗi gọi PMIS.", consecutiveErrors);
                break;
            }

            stats.Checked++;
            var isSubstation = owner.InfraTypeId == 1;
            try
            {
                int? total;
                if (IsInProgress(state) && state!.RemoteTotal != null)
                    total = state.RemoteTotal; // đang quét dở — không cần đếm lại
                else
                    total = await _pmisClient.GetDocumentTotalAsync(isSubstation, owner.PmisCode);
                if (total == null) { stats.Skip++; continue; }

                int? dbCount = null;
                if (state?.RemoteTotal == null)
                {
                    dbCounts ??= await _equipmentServiceClient.GetDocumentCountsByInfrastructureAsync();
                    dbCount = dbCounts.GetValueOrDefault(owner.PmisCode, 0);
                }

                var plan = DocumentOwnerSyncPlanner.Decide(state, total.Value, dbCount, DateTime.UtcNow, opt);
                Log.Debug("PmisDocumentListSyncJob: {Owner} → {Action} ({Reason})", owner.PmisCode, plan.Action, plan.Reason);

                var ok = await ExecutePlanAsync(owner, isSubstation, state, total.Value, dbCount, plan, historyId, budget, stats, allDetails);
                consecutiveErrors = ok ? 0 : consecutiveErrors + 1;
                // Full/Windowed đã tự ghi trạng thái (gồm LastCountAt=bây giờ) trước khi trả false — KHÔNG ghi đè bằng snapshot cũ ở đây
                // (làm mất ScanSkip/RemoteTotal vừa lưu). Delta/ngoại lệ tự touch ở nơi phát sinh.
                if (!ok) stats.Errors++;
                if (stats.Paused) break;
            }
            catch (Exception ex) when (IsCircuitOpen(ex))
            {
                // Mạch PMIS đang mở (đã lỗi dồn dập): không phải lỗi của owner — tạm dừng cả lượt, lượt sau thử lại.
                Log.Warning("PmisDocumentListSyncJob: circuit breaker PMIS đang mở — dừng lượt này để PMIS hồi phục.");
                stats.AddMessage("Dừng lượt: circuit breaker PMIS đang mở.");
                stats.Paused = true;
                break;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                stats.Errors++;
                Log.Warning(ex, "PmisDocumentListSyncJob: lỗi xử lý owner {Owner}.", owner.PmisCode);
                stats.AddMessage($"{owner.PmisCode}: {SyncErrorFormatter.FormatShort(ex)}");
                await TouchCountAsync(owner.PmisCode, state);
            }

            if (allDetails.Count >= 500)
            {
                await _syncHistoryRepository.InsertDetailsAsync(allDetails);
                allDetails.Clear();
            }
        }

        if (allDetails.Count > 0) await _syncHistoryRepository.InsertDetailsAsync(allDetails);
    }

    /// <summary>Thực hiện kế hoạch cho 1 owner và ghi trạng thái. Trả false nếu gặp lỗi gọi PMIS.</summary>
    private async Task<bool> ExecutePlanAsync(
        SyncedInfrastructurePmisCode owner, bool isSubstation, DocumentOwnerState? state, int total, int? dbCount,
        DocumentSyncPlan plan, string historyId, RunBudgetClock budget, RunStats stats, List<SyncHistoryDetail> allDetails)
    {
        var now = DateTime.UtcNow;
        var next = new DocumentOwnerState
        {
            PmisCode = owner.PmisCode,
            RemoteTotal = state?.RemoteTotal,
            LocalCount = state?.LocalCount,
            LastFetchAt = state?.LastFetchAt,
            LastCountAt = now,
            LastFullAt = state?.LastFullAt,
            ScanSkip = state?.ScanSkip
        };

        switch (plan.Action)
        {
            case DocumentSyncAction.Skip:
                stats.Skip++;
                next.RemoteTotal = total;
                await _stateRepository.UpsertDocumentStateAsync(OwnerStateType, next);
                return true;

            case DocumentSyncAction.Seed:
                stats.Seed++;
                next.RemoteTotal = total;
                next.LocalCount = dbCount ?? 0;
                next.LastFetchAt = now;
                next.ScanSkip = null;
                await _stateRepository.UpsertDocumentStateAsync(OwnerStateType, next);
                return true;

            case DocumentSyncAction.Delta:
            {
                stats.Delta++;
                var res = await _executionService.SyncDocumentsForInfrastructureOwnerAsync(owner.PmisCode, owner.InfraTypeId, historyId,
                    new DocumentScanOptions { UseCallBudget = false, TuNgay = plan.TuNgay, ShouldStop = () => budget.Exceeded });
                Accumulate(stats, res, allDetails);
                if (res.CircuitOpen) { PauseForCircuit(stats); return true; }
                if (res.Error != null || res.AllFailed)
                {
                    stats.AddMessage($"{owner.PmisCode}: {res.Error ?? "mọi tài liệu lưu lỗi"}");
                    await TouchCountAsync(owner.PmisCode, state); // lùi cuối hàng đợi, không chặn các owner khác
                    return false;
                }
                // Chưa quét hết (hết giờ) → KHÔNG ghi tổng mới: lượt sau vẫn thấy "tổng tăng" và lấy lại phần mới (upsert idempotent).
                // Không touch: chỉ bị ngân sách thời gian ngắt, owner vẫn nên được xét lại ngay lượt sau.
                if (!res.Completed) return true;
                next.RemoteTotal = total;
                next.LocalCount = (state?.LocalCount ?? 0) + res.Created;
                next.LastFetchAt = now;
                next.ScanSkip = null;
                await _stateRepository.UpsertDocumentStateAsync(OwnerStateType, next);
                return true;
            }

            case DocumentSyncAction.Full:
            {
                stats.Full++;
                var startSkip = state?.ScanSkip is > 0 ? state.ScanSkip.Value : 0;
                var res = await _executionService.SyncDocumentsForInfrastructureOwnerAsync(owner.PmisCode, owner.InfraTypeId, historyId,
                    new DocumentScanOptions { UseCallBudget = false, StartSkip = startSkip, ShouldStop = () => budget.Exceeded });
                Accumulate(stats, res, allDetails);
                if (res.CircuitOpen) { PauseForCircuit(stats); return true; }
                if (res.AllFailed && res.Error == null)
                {
                    // Mọi tài liệu của đoạn này lưu lỗi (vd EquipmentService ngừng): không ghi tiến triển, tính là lỗi owner.
                    stats.AddMessage($"{owner.PmisCode}: mọi tài liệu lưu lỗi");
                    await TouchCountAsync(owner.PmisCode, state);
                    return false;
                }
                next.RemoteTotal = total;

                // Lỗi lưu của các đoạn quét TRƯỚC (đã dừng giữa chừng) còn nhớ được qua ScanSkip − LocalCount: LocalCount luôn = số đã LƯU được.
                var priorFailed = startSkip > 0 ? Math.Max(startSkip - (state?.LocalCount ?? startSkip), 0) : 0;
                var totalFailed = priorFailed + res.Failed;
                if (res.Completed)
                {
                    // Có lỗi lưu (bất kỳ đoạn nào) thì KHÔNG đánh dấu quét xong theo chu kỳ 7 ngày: LastFullAt lùi để tối đa ~1 ngày sau quét lại
                    // (không lặp mỗi kỳ đếm vô hạn với tài liệu luôn lỗi — vd tên quá dài).
                    next.LocalCount = Math.Max(res.NextSkip - totalFailed, 0);
                    next.LastFetchAt = now;
                    next.LastFullAt = FullAtAfterSave(totalFailed, now);
                    next.ScanSkip = null;
                    await _stateRepository.UpsertDocumentStateAsync(OwnerStateType, next);
                    return true;
                }

                // Dừng giữa chừng (hết giờ) hoặc lỗi giữa chừng: lưu vị trí để lượt sau tiếp tục đúng chỗ.
                next.ScanSkip = res.NextSkip > 0 ? res.NextSkip : state?.ScanSkip;
                next.LocalCount = Math.Max(res.NextSkip - totalFailed, 0);
                await _stateRepository.UpsertDocumentStateAsync(OwnerStateType, next);
                if (res.Error != null) { stats.AddMessage($"{owner.PmisCode}: {res.Error}"); return false; }
                return true;
            }

            case DocumentSyncAction.Windowed:
            {
                stats.Windowed++;
                var (done, sum, error, hadFailures) = await RunWindowedAsync(owner, isSubstation, historyId, budget, stats, allDetails);
                next.RemoteTotal = total;
                if (stats.Paused) return true; // circuit PMIS mở giữa chừng — giữ nguyên trạng thái, lượt sau tiếp tục
                if (done)
                {
                    next.LocalCount = sum;
                    next.LastFetchAt = now;
                    next.LastFullAt = FullAtAfterSave(hadFailures ? 1 : 0, now); // có khoảng lưu lỗi → quét lại ~1 ngày sau (chỉ các khoảng chưa xong)
                    next.ScanSkip = null;
                    if (sum < total)
                        Log.Warning("PmisDocumentListSyncJob: owner {Owner} chia theo khoảng ngày nhận {Sum}/{Total} tài liệu — phần chênh có thể không có ngày hợp lệ để lọc.", owner.PmisCode, sum, total);
                }
                else
                {
                    next.ScanSkip = DocumentOwnerSyncPlanner.WindowedMarker;
                }
                await _stateRepository.UpsertDocumentStateAsync(OwnerStateType, next);
                if (error != null) { stats.AddMessage($"{owner.PmisCode}: {error}"); return false; }
                return true;
            }
        }

        return true;
    }

    /// <summary>Owner &gt; ngưỡng: quét theo từng NĂM (rồi từng THÁNG nếu năm đó vẫn &gt; ngưỡng). Mỗi khoảng có trạng thái riêng
    /// (DOC_WINDOW) nên dừng giữa chừng/chạy lại không làm lại khoảng đã xong. Trả (đã xong tất cả, tổng đã nhận, lỗi).</summary>
    private async Task<(bool Done, int Received, string? Error, bool HadFailures)> RunWindowedAsync(
        SyncedInfrastructurePmisCode owner, bool isSubstation, string historyId, RunBudgetClock budget, RunStats stats, List<SyncHistoryDetail> allDetails)
    {
        var opt = _options.Value;
        var windows = await _stateRepository.GetDocumentStatesByPrefixAsync(WindowStateType, owner.PmisCode + "|");
        var sum = 0;
        var hadFailures = false;
        var nowYear = DateTime.UtcNow.Year;

        async Task<(bool Ok, int Count, string? Error)> RunWindowAsync(string key, DateTime from, DateTime to, bool refetchAlways)
        {
            windows.TryGetValue(key, out var ws);
            if (ws?.LastFullAt != null && !refetchAlways && ws.ScanSkip is null or 0) return (true, ws.LocalCount ?? 0, null);

            if (budget.Exceeded) return (false, 0, null);
            var count = await _pmisClient.GetDocumentTotalAsync(isSubstation, owner.PmisCode, from, to) ?? 0;
            var wnext = new DocumentOwnerState { PmisCode = key, RemoteTotal = count, LastCountAt = DateTime.UtcNow, LastFullAt = ws?.LastFullAt };
            if (count == 0)
            {
                wnext.LocalCount = 0; wnext.LastFullAt = DateTime.UtcNow; wnext.LastFetchAt = DateTime.UtcNow;
                await _stateRepository.UpsertDocumentStateAsync(WindowStateType, wnext);
                return (true, 0, null);
            }

            var startSkip = ws?.ScanSkip is > 0 ? ws.ScanSkip.Value : 0;
            var res = await _executionService.SyncDocumentsForInfrastructureOwnerAsync(owner.PmisCode, owner.InfraTypeId, historyId,
                new DocumentScanOptions { UseCallBudget = false, TuNgay = from, DenNgay = to, StartSkip = startSkip, ShouldStop = () => budget.Exceeded });
            Accumulate(stats, res, allDetails);
            if (res.CircuitOpen) { PauseForCircuit(stats); return (false, 0, null); }

            // Lỗi lưu của đoạn quét TRƯỚC của cùng khoảng này: ScanSkip − LocalCount (LocalCount luôn = số đã lưu được).
            var priorFailed = startSkip > 0 ? Math.Max(startSkip - (ws?.LocalCount ?? startSkip), 0) : 0;
            var totalFailed = priorFailed + res.Failed;
            if (res.Completed || res.Truncated)
            {
                // Có tài liệu lưu lỗi (bất kỳ đoạn nào) → khoảng này chưa "xong" (LastFullAt=null) để lượt windowed sau làm lại đúng khoảng này.
                if (totalFailed > 0) hadFailures = true;
                wnext.LocalCount = Math.Max(res.NextSkip - totalFailed, 0);
                wnext.LastFullAt = totalFailed > 0 ? null : DateTime.UtcNow;
                wnext.LastFetchAt = DateTime.UtcNow; wnext.ScanSkip = null;
                await _stateRepository.UpsertDocumentStateAsync(WindowStateType, wnext);
                return (true, wnext.LocalCount ?? 0, null);
            }

            wnext.ScanSkip = res.NextSkip;
            wnext.LocalCount = Math.Max(res.NextSkip - totalFailed, 0);
            await _stateRepository.UpsertDocumentStateAsync(WindowStateType, wnext);
            return (false, 0, res.Error);
        }

        for (var year = opt.WindowStartYear; year <= nowYear; year++)
        {
            var (from, to) = DocumentOwnerSyncPlanner.WindowRange(year);
            var yearKey = DocumentOwnerSyncPlanner.WindowKey(owner.PmisCode, year);

            windows.TryGetValue(yearKey, out var yws);
            // Năm đã xong (không phải năm hiện tại) → dùng lại số đã nhận; năm có tổng > ngưỡng đã được chia tháng (RemoteTotal > ngưỡng, LastFullAt null).
            if (yws?.LastFullAt != null && year < nowYear && yws.ScanSkip is null or 0) { sum += yws.LocalCount ?? 0; continue; }

            if (budget.Exceeded) return (false, sum, null, hadFailures);

            int yearTotal;
            if (yws?.RemoteTotal is > 0 && yws.RemoteTotal > opt.WindowThreshold && year < nowYear) yearTotal = yws.RemoteTotal.Value; // đã biết là năm lớn
            else yearTotal = await _pmisClient.GetDocumentTotalAsync(isSubstation, owner.PmisCode, from, to) ?? 0;

            if (yearTotal <= opt.WindowThreshold)
            {
                var (ok, count, error) = await RunWindowAsync(yearKey, from, to, refetchAlways: year == nowYear);
                if (!ok) return (false, sum, error, hadFailures);
                sum += count;
                continue;
            }

            // Năm quá lớn: chia theo tháng. Ghi nhận năm là "lớn" để lượt sau không đếm lại.
            await _stateRepository.UpsertDocumentStateAsync(WindowStateType,
                new DocumentOwnerState { PmisCode = yearKey, RemoteTotal = yearTotal, LastCountAt = DateTime.UtcNow, LastFullAt = null });
            var monthSum = 0;
            for (var month = 1; month <= 12; month++)
            {
                if (year == nowYear && month > DateTime.UtcNow.Month) break;
                var (mf, mt) = DocumentOwnerSyncPlanner.WindowRange(year, month);
                var (ok, count, error) = await RunWindowAsync(DocumentOwnerSyncPlanner.WindowKey(owner.PmisCode, year, month), mf, mt, refetchAlways: year == nowYear && month == DateTime.UtcNow.Month);
                if (!ok) return (false, sum + monthSum, error, hadFailures);
                monthSum += count;
            }
            sum += monthSum;
            if (year < nowYear && !hadFailures)
                await _stateRepository.UpsertDocumentStateAsync(WindowStateType,
                    new DocumentOwnerState { PmisCode = yearKey, RemoteTotal = yearTotal, LocalCount = monthSum, LastCountAt = DateTime.UtcNow, LastFullAt = DateTime.UtcNow, LastFetchAt = DateTime.UtcNow });
        }

        return (true, sum, null, hadFailures);
    }

    /// <summary>Owner lỗi/chưa xong: cập nhật LAST_DOC_COUNT_AT = bây giờ (giữ nguyên các trường khác) để owner đó lùi về cuối hàng đợi
    /// thay vì luôn đứng đầu và chặn các owner khác. Owner đang quét dở vẫn được ưu tiên theo ScanSkip.</summary>
    private async Task TouchCountAsync(string code, DocumentOwnerState? state)
    {
        try
        {
            var touched = new DocumentOwnerState
            {
                PmisCode = code, RemoteTotal = state?.RemoteTotal, LocalCount = state?.LocalCount, LastFetchAt = state?.LastFetchAt,
                LastCountAt = DateTime.UtcNow, LastFullAt = state?.LastFullAt, ScanSkip = state?.ScanSkip
            };
            await _stateRepository.UpsertDocumentStateAsync(OwnerStateType, touched);
        }
        catch (Exception ex) { Log.Debug(ex, "PmisDocumentListSyncJob: không ghi được mốc đếm của {Owner}.", code); }
    }

    private static bool IsCircuitOpen(Exception ex) =>
        ex is Polly.CircuitBreaker.BrokenCircuitException || ex.InnerException is Polly.CircuitBreaker.BrokenCircuitException;

    private static void PauseForCircuit(RunStats stats)
    {
        stats.Paused = true;
        stats.AddMessage("Dừng lượt: circuit breaker PMIS đang mở.");
        Log.Warning("PmisDocumentListSyncJob: circuit breaker PMIS đang mở giữa lúc quét — dừng lượt để PMIS hồi phục.");
    }

    /// <summary>Mốc "quét đầy đủ gần nhất" ghi sau khi quét xong: bình thường = bây giờ; có tài liệu lưu lỗi thì lùi (FullRescanIntervalDays − 1) ngày
    /// để planner quét lại sau ~1 ngày — đủ sớm để vá lỗi tạm, đủ thưa để không kéo lại owner mỗi kỳ đếm với tài liệu luôn lỗi.</summary>
    private DateTime FullAtAfterSave(int failedCount, DateTime nowUtc) =>
        failedCount > 0 ? nowUtc.AddDays(-Math.Max(_options.Value.FullRescanIntervalDays - 1, 0)) : nowUtc;

    /// <summary>Owner đang quét dở (full theo skip, hoặc theo khoảng ngày).</summary>
    private static bool IsInProgress(DocumentOwnerState? state) =>
        state?.ScanSkip is > 0 or DocumentOwnerSyncPlanner.WindowedMarker;

    private static void Accumulate(RunStats stats, DocumentOwnerSyncResult res, List<SyncHistoryDetail> allDetails)
    {
        stats.Total += res.Received;
        stats.Failed += res.Failed;
        stats.Created += res.Created;
        stats.Warnings += res.Warnings;
        allDetails.AddRange(res.Details);
    }
}
