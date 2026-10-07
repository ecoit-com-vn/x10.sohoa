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
/// Pha 2 của đồng bộ tài liệu PMIS: tải FILE VẬT LÝ, tách hẳn khỏi pha đồng bộ danh sách (xem
/// PmisSyncExecutionService — pha đó chỉ lưu metadata + URL, FILE_STATUS=PENDING). Mỗi phút job lấy tối đa
/// <c>BatchSize</c> tài liệu đang chờ (đã tới hạn thử lại) từ EquipmentService, tải song song (số luồng do
/// <see cref="DocumentFileThrottle"/> điều tốc, tối đa <c>MaxParallel</c>) rồi đẩy sang EquipmentService DẠNG LUỒNG
/// (không base64, không nạp cả file vào RAM; file lớn đi qua file tạm). Kết quả lỗi được phân loại: lỗi riêng của tài liệu
/// (404/JSON/rỗng/quá lớn) tính vào số lần thử; lỗi tạm thời của PMIS (5xx/429/timeout) KHÔNG tính, chỉ hẹn thử lại sau ít
/// phút. Trước khi gửi byte, hỏi EquipmentService đã có file trùng SHA-256 chưa — có thì gắn lại object cũ (chống trùng).
///
/// RedLock theo tên job chặn nhiều pod cùng tải một lô; [DisallowConcurrentExecution] chặn chồng lượt trong 1 pod
/// (lượt chậm hơn 1 phút thì lượt sau bỏ qua, không dồn).
/// </summary>
[DisallowConcurrentExecution]
public class PmisDocumentFileDownloadJob : IJob
{
    // Điều tốc dùng chung giữa các lượt (Quartz tạo job mới mỗi lượt) — tĩnh để giữ trạng thái.
    private static readonly object ThrottleGate = new();
    private static DocumentFileThrottle? _throttle;

    private readonly ISyncConfigRepository _syncConfigRepository;
    private readonly IEquipmentServiceClient _equipmentServiceClient;
    private readonly IPmisClient _pmisClient;
    private readonly IDistributedLockFactory _lockFactory;
    private readonly IOptions<PmisDocumentFileOptions> _options;

    public PmisDocumentFileDownloadJob(
        ISyncConfigRepository syncConfigRepository,
        IEquipmentServiceClient equipmentServiceClient,
        IPmisClient pmisClient,
        IDistributedLockFactory lockFactory,
        IOptions<PmisDocumentFileOptions> options)
    {
        _syncConfigRepository = syncConfigRepository;
        _equipmentServiceClient = equipmentServiceClient;
        _pmisClient = pmisClient;
        _lockFactory = lockFactory;
        _options = options;
    }

    private DocumentFileThrottle GetThrottle()
    {
        lock (ThrottleGate)
            return _throttle ??= new DocumentFileThrottle(Math.Min(_options.Value.InitialParallel, _options.Value.MaxParallel));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var opt = _options.Value;

            // Tôn trọng công tắc đồng bộ của admin: cả Trạm lẫn Đường dây đều đang tắt thì không tải file
            // (tài liệu chỉ phát sinh từ 2 đối tượng này) — tránh tiếp tục gọi PMIS khi admin chủ ý dừng.
            var substationConfig = await _syncConfigRepository.GetByObjectTypeAsync(SyncObjectType.Substation);
            var lineConfig = await _syncConfigRepository.GetByObjectTypeAsync(SyncObjectType.TransmissionLine);
            if (substationConfig is not { IsEnabled: true } && lineConfig is not { IsEnabled: true })
            {
                Log.Information("PmisDocumentFileDownloadJob: bỏ qua lượt này vì cả 2 switch Trạm biến áp/Đường dây đều đang tắt.");
                return;
            }

            // API DOCUMENT_FILE_DOWNLOAD (màn Cấu hình kết nối API) chưa nhập URL / đang tắt → không gọi PMIS và KHÔNG tăng
            // số lần thử của tài liệu nào (admin chủ động tắt hoặc chưa cấu hình, không phải lỗi của tài liệu).
            if (!await _pmisClient.IsDocumentFileEndpointActiveAsync())
            {
                Log.Information("PmisDocumentFileDownloadJob: bỏ qua lượt này vì API {ApiCode} chưa cấu hình URL hoặc đang tắt.", PmisApiCodes.DocumentFileDownload);
                return;
            }

            var throttle = GetThrottle();
            if (throttle.InCooldown(DateTime.UtcNow))
            {
                Log.Information("PmisDocumentFileDownloadJob: đang nghỉ sau khi PMIS trả lỗi tạm thời dồn dập (số luồng hiện {Parallel}), bỏ qua lượt này.", throttle.Parallel);
                return;
            }

            await using var redLock = await _lockFactory.CreateLockAsync(
                "sync:lock:pmis:document-files", TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));
            if (!redLock.IsAcquired)
            {
                Log.Information("PmisDocumentFileDownloadJob: lô tải file đang chạy ở tiến trình khác, bỏ qua lượt này.");
                return;
            }

            var pending = await _equipmentServiceClient.GetPendingDocumentFilesAsync(Math.Max(1, opt.BatchSize), opt.ExcludedPrefixList);
            if (pending.Count == 0)
            {
                // Mức Debug: trạng thái bình thường khi hàng đợi đã tải hết — không gây ồn log mỗi phút. Giúp phân biệt "job có chạy
                // nhưng không có gì làm" với "job không được Quartz đăng ký" (phát hiện thật 2026-10-01).
                Log.Debug("PmisDocumentFileDownloadJob: không có tài liệu nào đang chờ tải.");
                return;
            }

            throttle.ResetBatch();
            var parallel = Math.Max(1, Math.Min(throttle.Parallel, opt.MaxParallel));
            var stats = new BatchStats();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await Parallel.ForEachAsync(
                pending,
                new ParallelOptions { MaxDegreeOfParallelism = parallel, CancellationToken = context.CancellationToken },
                async (doc, ct) =>
                {
                    // Dừng sớm: lỗi xác thực toàn cục, hoặc PMIS trả lỗi tạm thời dồn dập — không đụng tài liệu còn lại của lô.
                    if (stats.Unauthorized || throttle.ShouldAbortBatch)
                    {
                        Interlocked.Increment(ref stats.Skipped);
                        return;
                    }
                    await ProcessOneAsync(doc, opt, throttle, stats, ct);
                });

            Log.Information("PmisDocumentFileDownloadJob: {Total} tài liệu / {Parallel} luồng / {Seconds:0}s — {Ok} thành công ({Dedup} dùng lại file trùng, {MB:0.0} MB), {Permanent} lỗi riêng tài liệu, {Transient} lỗi tạm thời của PMIS, {Internal} lỗi lưu nội bộ, {Skipped} bỏ qua.",
                pending.Count, parallel, watch.Elapsed.TotalSeconds, stats.Ok, stats.Dedup, Interlocked.Read(ref stats.Bytes) / 1048576.0, stats.Permanent, stats.Transient, stats.Internal, stats.Skipped);

            if (stats.Unauthorized)
                Log.Error("PmisDocumentFileDownloadJob: PMIS trả 401/403 cho API {ApiCode} — kiểm tra header xác thực ở màn Cấu hình kết nối API. Đã dừng cả lô, không tính lần thử của tài liệu nào.", PmisApiCodes.DocumentFileDownload);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisDocumentFileDownloadJob: lỗi khi xử lý lô tải file tài liệu PMIS.");
        }
    }

    private sealed class BatchStats
    {
        public int Ok, Dedup, Permanent, Transient, Internal, Skipped;
        public long Bytes;
        public volatile bool Unauthorized;
    }

    /// <summary>Best-effort: báo lỗi lưu nội bộ như lỗi TẠM THỜI (không tính lần thử) để EquipmentService hẹn lại — không throw.</summary>
    private async Task TryReportInternalFailureAsync(string code, string message, PmisDocumentFileOptions opt, CancellationToken ct)
    {
        try { await _equipmentServiceClient.ReportDocumentFileFailureAsync(code, true, message, opt.TransientRetryMinutes, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { Log.Debug(ex, "PmisDocumentFileDownloadJob: không báo được lỗi nội bộ của {Code}.", code); }
    }

    private async Task ProcessOneAsync(PendingPmisDocumentFile doc, PmisDocumentFileOptions opt, DocumentFileThrottle throttle, BatchStats stats, CancellationToken ct)
    {
        DocumentFileDownloadResult? result = null;
        try
        {
            result = await _pmisClient.DownloadDocumentFileByCodeAsync(doc.PmisDocumentCode, opt.MaxBytes, opt.SpoolThresholdBytes, ct);
            switch (result.Kind)
            {
                case DocumentFileOutcomeKind.Ok:
                    throttle.Record(false, opt.MaxParallel, DateTime.UtcNow);
                    try
                    {
                        if (await _equipmentServiceClient.TryAttachExistingFileByHashAsync(doc.PmisDocumentCode, result.Sha256Hex!, result.Length, ct))
                        {
                            Interlocked.Increment(ref stats.Dedup);
                        }
                        else
                        {
                            await using var content = result.OpenRead();
                            if (!await _equipmentServiceClient.UploadDocumentFileAsync(doc.PmisDocumentCode, content, result.Length, result.Sha256Hex!, ct))
                            {
                                Interlocked.Increment(ref stats.Internal);
                                await TryReportInternalFailureAsync(doc.PmisDocumentCode, "EquipmentService không lưu được file.", opt, ct);
                                return;
                            }
                        }
                        Interlocked.Increment(ref stats.Ok);
                        Interlocked.Add(ref stats.Bytes, result.Length);
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        // Lỗi phía EquipmentService/MinIO (kể cả HttpClient timeout 15 phút = TaskCanceledException khi ct chưa huỷ) — KHÔNG phải lỗi của tài liệu hay PMIS: không ghi lỗi tài liệu, lượt sau thử lại.
                        Interlocked.Increment(ref stats.Internal);
                        Log.Warning(ex, "PmisDocumentFileDownloadJob: lưu file tài liệu {Code} sang EquipmentService thất bại (không tính lần thử).", doc.PmisDocumentCode);
                        // Hẹn thử lại sau (backoff) — nếu không, lô cũ nhất bị tải lại từ PMIS mỗi phút khi MinIO/EquipmentService hỏng kéo dài.
                        await TryReportInternalFailureAsync(doc.PmisDocumentCode, "Lỗi lưu nội bộ: " + SyncErrorFormatter.FormatShort(ex), opt, ct);
                    }
                    return;

                case DocumentFileOutcomeKind.Permanent:
                    throttle.Record(false, opt.MaxParallel, DateTime.UtcNow);
                    Interlocked.Increment(ref stats.Permanent);
                    await _equipmentServiceClient.ReportDocumentFileFailureAsync(doc.PmisDocumentCode, false, result.Reason, opt.TransientRetryMinutes, ct);
                    return;

                case DocumentFileOutcomeKind.Transient:
                    throttle.Record(true, opt.MaxParallel, DateTime.UtcNow);
                    Interlocked.Increment(ref stats.Transient);
                    await _equipmentServiceClient.ReportDocumentFileFailureAsync(doc.PmisDocumentCode, true, result.Reason, opt.TransientRetryMinutes, ct);
                    return;

                case DocumentFileOutcomeKind.Unauthorized:
                    stats.Unauthorized = true;
                    return;

                case DocumentFileOutcomeKind.CircuitOpen:
                    // Mạch đang mở = PMIS vừa lỗi dồn dập: ghi vào điều tốc để hạ số luồng/nghỉ, không đụng tài liệu.
                    throttle.Record(true, opt.MaxParallel, DateTime.UtcNow);
                    Interlocked.Increment(ref stats.Skipped);
                    return;

                default: // NotConfigured: chưa gọi PMIS thật — để nguyên, lượt sau thử lại.
                    Interlocked.Increment(ref stats.Skipped);
                    return;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Interlocked.Increment(ref stats.Skipped);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref stats.Internal);
            Log.Warning(ex, "PmisDocumentFileDownloadJob: lỗi xử lý tài liệu {Code}.", doc.PmisDocumentCode);
            await TryReportInternalFailureAsync(doc.PmisDocumentCode, "Lỗi xử lý: " + SyncErrorFormatter.FormatShort(ex), opt, CancellationToken.None);
        }
        finally
        {
            if (result != null) await result.DisposeAsync();
        }
    }
}
