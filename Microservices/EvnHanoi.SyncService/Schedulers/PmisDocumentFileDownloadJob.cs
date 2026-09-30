using EvnHanoi.SyncService.Clients;
using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Models.Internal;
using EvnHanoi.SyncService.Repositories;
using Quartz;
using RedLockNet;
using Serilog;

namespace EvnHanoi.SyncService.Schedulers;

/// <summary>
/// Pha 2 của đồng bộ tài liệu PMIS: tải FILE VẬT LÝ, tách hẳn khỏi pha đồng bộ danh sách (xem
/// PmisSyncExecutionService — pha đó chỉ lưu metadata + URL, FILE_STATUS=PENDING). Mỗi phút job lấy tối đa
/// <see cref="BatchSize"/> tài liệu đang chờ (đã tới hạn thử lại) từ EquipmentService, tải song song tối đa
/// <see cref="MaxParallelDownloads"/> file rồi gửi lại (attach-file). Tải lỗi thì EquipmentService ghi lý do
/// và đặt lịch thử lại theo backoff — 1 file PMIS chậm/treo chỉ chiếm 1 slot song song, không chặn đồng bộ
/// danh sách hay các file khác.
///
/// RedLock theo tên job chặn nhiều pod cùng tải một lô; [DisallowConcurrentExecution] chặn chồng lượt trong
/// 1 pod (lượt chậm hơn 1 phút thì lượt sau bỏ qua, không dồn).
/// </summary>
[DisallowConcurrentExecution]
public class PmisDocumentFileDownloadJob : IJob
{
    private const int BatchSize = 40;
    private const int MaxParallelDownloads = 4;

    private readonly ISyncConfigRepository _syncConfigRepository;
    private readonly IEquipmentServiceClient _equipmentServiceClient;
    private readonly IPmisClient _pmisClient;
    private readonly IDistributedLockFactory _lockFactory;

    public PmisDocumentFileDownloadJob(
        ISyncConfigRepository syncConfigRepository,
        IEquipmentServiceClient equipmentServiceClient,
        IPmisClient pmisClient,
        IDistributedLockFactory lockFactory)
    {
        _syncConfigRepository = syncConfigRepository;
        _equipmentServiceClient = equipmentServiceClient;
        _pmisClient = pmisClient;
        _lockFactory = lockFactory;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            // Tôn trọng công tắc đồng bộ của admin: cả Trạm lẫn Đường dây đều đang tắt thì không tải file
            // (tài liệu chỉ phát sinh từ 2 đối tượng này) — tránh tiếp tục gọi PMIS khi admin chủ ý dừng.
            var substationConfig = await _syncConfigRepository.GetByObjectTypeAsync(SyncObjectType.Substation);
            var lineConfig = await _syncConfigRepository.GetByObjectTypeAsync(SyncObjectType.TransmissionLine);
            if (substationConfig is not { IsEnabled: true } && lineConfig is not { IsEnabled: true }) return;

            await using var redLock = await _lockFactory.CreateLockAsync(
                "sync:lock:pmis:document-files", TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));
            if (!redLock.IsAcquired)
            {
                Log.Information("PmisDocumentFileDownloadJob: lô tải file đang chạy ở tiến trình khác, bỏ qua lượt này.");
                return;
            }

            var pending = await _equipmentServiceClient.GetPendingDocumentFilesAsync(BatchSize);
            if (pending.Count == 0) return;

            int ok = 0, failed = 0;
            await Parallel.ForEachAsync(
                pending,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDownloads, CancellationToken = context.CancellationToken },
                async (doc, _) =>
                {
                    if (await ProcessOneAsync(doc)) Interlocked.Increment(ref ok);
                    else Interlocked.Increment(ref failed);
                });

            Log.Information("PmisDocumentFileDownloadJob: xử lý {Total} tài liệu chờ tải file — {Ok} thành công, {Failed} lỗi (sẽ thử lại theo backoff).",
                pending.Count, ok, failed);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisDocumentFileDownloadJob: lỗi khi xử lý lô tải file tài liệu PMIS.");
        }
    }

    private async Task<bool> ProcessOneAsync(PendingPmisDocumentFile doc)
    {
        try
        {
            var (bytes, errorReason) = await _pmisClient.DownloadDocumentFileAsync(
                doc.FileUrl, doc.FileSourceApi ?? "SUBSTATION_DOCUMENT_LIST");
            var hasFile = bytes is { Length: > 0 };
            await _equipmentServiceClient.AttachDocumentFileAsync(new AttachPmisDocumentFileRequest
            {
                PmisDocumentCode = doc.PmisDocumentCode,
                FileBase64 = hasFile ? Convert.ToBase64String(bytes!) : null,
                ErrorMessage = hasFile ? null : (errorReason ?? "PMIS trả về file rỗng.")
            });
            return hasFile;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PmisDocumentFileDownloadJob: lỗi xử lý tài liệu {Code}.", doc.PmisDocumentCode);
            return false;
        }
    }
}
