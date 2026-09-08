using EvnHanoi.SyncService.Repositories;
using Quartz;
using Serilog;

namespace EvnHanoi.SyncService.Schedulers;

/// <summary>
/// Dọn PMIS_API_CALL_LOG (lịch sử gọi PMIS thật, ghi ở PmisClient) cũ hơn <see cref="RetentionDays"/>
/// ngày — chạy mỗi 24h (xem Program.cs). Bảng này có thể phình rất nhanh vì mỗi trang trong 1 lượt
/// đồng bộ tự động là 1 dòng log; không dọn định kỳ dễ lặp lại sự cố đầy tablespace đã gặp với
/// SYNC_HISTORY_DETAIL.
/// </summary>
public class PmisApiCallLogCleanupJob : IJob
{
    private const int RetentionDays = 30;

    private readonly IPmisApiCallLogRepository _repository;

    public PmisApiCallLogCleanupJob(IPmisApiCallLogRepository repository)
    {
        _repository = repository;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var deleted = await _repository.DeleteOlderThanAsync(RetentionDays);
            if (deleted > 0)
                Log.Information("PmisApiCallLogCleanupJob: đã xoá {Count} dòng log gọi API PMIS cũ hơn {Days} ngày.", deleted, RetentionDays);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisApiCallLogCleanupJob: lỗi khi dọn log cũ.");
        }
    }
}
