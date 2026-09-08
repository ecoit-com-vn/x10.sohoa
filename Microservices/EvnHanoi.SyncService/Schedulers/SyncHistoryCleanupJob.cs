using EvnHanoi.SyncService.Repositories;
using Quartz;
using Serilog;

namespace EvnHanoi.SyncService.Schedulers;

/// <summary>
/// Dọn SYNC_HISTORY (kéo theo SYNC_HISTORY_DETAIL qua ON DELETE CASCADE) cũ hơn
/// <see cref="RetentionDays"/> ngày — chạy mỗi 24h (xem Program.cs). Trước đây bảng này KHÔNG có cơ
/// chế dọn nào, đã từng gây tràn tablespace (ORA-01653) — cùng vấn đề đã gặp và sửa cho
/// PMIS_API_CALL_LOG, xem PmisApiCallLogCleanupJob.
/// </summary>
public class SyncHistoryCleanupJob : IJob
{
    private const int RetentionDays = 30;

    private readonly ISyncHistoryRepository _repository;

    public SyncHistoryCleanupJob(ISyncHistoryRepository repository)
    {
        _repository = repository;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var deleted = await _repository.DeleteOlderThanAsync(RetentionDays);
            if (deleted > 0)
                Log.Information("SyncHistoryCleanupJob: đã xoá {Count} dòng SYNC_HISTORY (kèm chi tiết) cũ hơn {Days} ngày.", deleted, RetentionDays);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SyncHistoryCleanupJob: lỗi khi dọn lịch sử đồng bộ cũ.");
        }
    }
}
