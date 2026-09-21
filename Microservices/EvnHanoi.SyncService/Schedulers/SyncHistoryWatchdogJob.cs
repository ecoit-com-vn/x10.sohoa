using EvnHanoi.SyncService.Repositories;
using Quartz;
using Serilog;

namespace EvnHanoi.SyncService.Schedulers;

/// <summary>
/// Dọn các dòng SYNC_HISTORY bị kẹt ở RUNNING quá lâu. Xảy ra khi pod SyncService bị dừng đột ngột
/// (crash/OOMKilled/rollout) đúng lúc PmisScheduledSyncJob đang chạy: RedLock tự nhả khoá sau tối đa
/// 10 phút (xem PmisScheduledSyncJob) nên lượt kế tiếp vẫn chạy bình thường, nhưng dòng SYNC_HISTORY
/// của lượt bị crash thì không còn ai gọi CompleteAsync — đứng ở RUNNING vĩnh viễn, làm sai lệch màn
/// hình lịch sử đồng bộ. Cùng ngưỡng với OcrJobWatchdogService (EquipmentService): quét mỗi 5 phút,
/// ngưỡng treo 30 phút — dài hơn hẳn TTL 10 phút của RedLock nên không đánh nhầm 1 lượt đang chạy thật.
/// </summary>
public class SyncHistoryWatchdogJob : IJob
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(30);

    private readonly ISyncHistoryRepository _repository;

    public SyncHistoryWatchdogJob(ISyncHistoryRepository repository)
    {
        _repository = repository;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var affected = await _repository.FailStaleRunningAsync(
                StaleThreshold,
                $"Tự động đánh dấu thất bại: RUNNING quá {StaleThreshold.TotalMinutes:0} phút không hoàn tất — có thể do khối lượng dữ liệu lớn hoặc SyncService bị dừng đột ngột giữa lượt chạy. Vui lòng chạy lại; nếu lỗi lặp lại nhiều lần, kiểm tra log SyncService.");

            if (affected > 0)
                Log.Warning("SyncHistoryWatchdogJob: đã đánh dấu FAILED cho {Count} dòng SYNC_HISTORY bị kẹt RUNNING quá {Minutes} phút.", affected, StaleThreshold.TotalMinutes);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SyncHistoryWatchdogJob: lỗi khi dọn SYNC_HISTORY bị kẹt RUNNING.");
        }
    }
}
