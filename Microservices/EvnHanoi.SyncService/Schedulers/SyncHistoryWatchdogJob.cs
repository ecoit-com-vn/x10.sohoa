using EvnHanoi.SyncService.Repositories;
using Quartz;
using Serilog;

namespace EvnHanoi.SyncService.Schedulers;

/// <summary>
/// Dọn các dòng SYNC_HISTORY bị kẹt ở RUNNING quá lâu. Xảy ra khi pod SyncService bị dừng đột ngột
/// (crash/OOMKilled/rollout) đúng lúc PmisScheduledSyncJob đang chạy — dòng SYNC_HISTORY của lượt bị crash
/// không còn ai gọi CompleteAsync, đứng ở RUNNING vĩnh viễn, làm sai lệch màn hình lịch sử đồng bộ.
///
/// Ngưỡng 60 phút KHÔNG còn dựa vào TTL RedLock (RedLockNet.SERedis tự động gia hạn khoá theo chu kỳ
/// trong suốt thời gian tiến trình còn sống — xem comment tại RedLock trong PmisScheduledSyncJob — nên TTL
/// không giới hạn thời lượng 1 lượt chạy hợp lệ, và trước đây lấy nó làm cơ sở chọn ngưỡng là SAI). Chọn
/// độc lập: đủ RỘNG để không đánh FAILED oan 1 lượt Equipment hợp lệ đang chạy thật (lồng 2 vòng phân
/// trang qua hàng chục nghìn Trạm/Đường dây cha, mỗi cha lại round-trip PMIS tuần tự — có thể mất hàng
/// chục phút với khối lượng dữ liệu thật đã ghi nhận, xem PmisScheduledSyncJob.MaxTotalRecords), nhưng vẫn
/// đủ HẸP để không để 1 lượt crash thật hiện RUNNING quá lâu trên màn hình. Cùng tần suất quét (5 phút) với
/// OcrJobWatchdogService (EquipmentService).
/// </summary>
public class SyncHistoryWatchdogJob : IJob
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(60);

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
