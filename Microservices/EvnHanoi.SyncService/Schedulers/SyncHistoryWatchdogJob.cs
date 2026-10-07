using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Services;
using Microsoft.Extensions.Options;
using Quartz;
using Serilog;

namespace EvnHanoi.SyncService.Schedulers;

/// <summary>
/// Dọn các dòng SYNC_HISTORY bị kẹt ở RUNNING quá lâu. Xảy ra khi pod SyncService bị dừng đột ngột
/// (crash/OOMKilled/rollout) đúng lúc PmisScheduledSyncJob đang chạy — dòng SYNC_HISTORY của lượt bị crash
/// không còn ai gọi CompleteAsync, đứng ở RUNNING vĩnh viễn, làm sai lệch màn hình lịch sử đồng bộ.
///
/// Ngưỡng KHÔNG còn cố định 60 phút: tính riêng cho từng dòng theo tần suất cấu hình của đối tượng (Trạm/Đường dây/
/// Thiết bị) = ngân sách thời gian của lượt (tần suất − đệm, xem <see cref="SyncRunBudget"/>) + 15 phút. Lượt tự
/// dừng mềm khi hết ngân sách nên chỉ lượt thật sự chết mới vượt ngưỡng này. Lượt thủ công / dòng không gắn cấu hình
/// lịch giữ ngưỡng 60 phút. Không dựa vào TTL RedLock (RedLockNet.SERedis tự gia hạn khoá trong lúc tiến trình còn sống).
/// Quét 5 phút/lần, giống OcrJobWatchdogService (EquipmentService).
/// </summary>
public class SyncHistoryWatchdogJob : IJob
{
    private readonly ISyncHistoryRepository _repository;
    private readonly IOptions<SyncScheduleOptions> _options;

    public SyncHistoryWatchdogJob(ISyncHistoryRepository repository, IOptions<SyncScheduleOptions> options)
    {
        _repository = repository;
        _options = options;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var now = DateTime.UtcNow;
            var failed = 0;
            foreach (var row in await _repository.GetRunningAsync())
            {
                var staleAfter = SyncRunBudget.StaleAfter(row.SyncType, row.FrequencyValue, row.FrequencyUnit, _options.Value);
                if (now - row.StartTime < staleAfter) continue;

                var message = $"Tự động đánh dấu thất bại: RUNNING quá {staleAfter.TotalMinutes:0} phút không hoàn tất — có thể SyncService bị dừng đột ngột giữa lượt chạy. Vui lòng chạy lại; nếu lỗi lặp lại nhiều lần, kiểm tra log SyncService.";
                if (await _repository.FailRunningAsync(row.Id, message))
                {
                    failed++;
                    Log.Warning("SyncHistoryWatchdogJob: đánh dấu FAILED dòng SYNC_HISTORY {Id} ({ObjectType}, {SyncType}) kẹt RUNNING quá {Minutes:0} phút.",
                        row.Id, row.ObjectType, row.SyncType, staleAfter.TotalMinutes);
                }
            }

            if (failed > 0)
                Log.Warning("SyncHistoryWatchdogJob: đã đánh dấu FAILED {Count} dòng SYNC_HISTORY bị kẹt RUNNING.", failed);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SyncHistoryWatchdogJob: lỗi khi dọn SYNC_HISTORY bị kẹt RUNNING.");
        }
    }
}
