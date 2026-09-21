using EvnHanoi.SyncService.Models;
using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Services;
using Quartz;
using Serilog;

namespace EvnHanoi.SyncService.Schedulers;

/// <summary>
/// Chạy nền, ĐỘC LẬP với mọi lượt đồng bộ Đường dây (tách ra khỏi PmisScheduledSyncJob.RunLineAsync và
/// PmisManualSyncController.Save — trước đây gọi BackfillLineParentsAsync inline ở cuối MỖI lượt sync,
/// từng gây treo RUNNING quá 30 phút thật trên production khi số nhánh "mồ côi" tồn đọng nhiều, xem
/// comment MaxBackfillPerRun trong PmisSyncExecutionService). Tự khớp lại ParentInfrastructureId/GridTypeId
/// cho các Đường dây/nhánh còn thiếu (xem InfrastructureRepository.GetLinesNeedingBackfillAsync) mỗi khi
/// tick — không cần RedLock: UpdateParentIdsAsync chỉ SET lại đúng giá trị đã tính được, 2 pod cùng chạy
/// trùng lúc chỉ lãng phí 1 lượt tính toán thừa, không có tác dụng phụ sai lệch dữ liệu (khác
/// PmisScheduledSyncJob cần RedLock vì nó gọi PMIS thật, chạy trùng sẽ lãng phí gọi API bên ngoài).
/// </summary>
public class LineParentBackfillJob : IJob
{
    private readonly IPmisSyncExecutionService _executionService;
    private readonly ISyncHistoryRepository _syncHistoryRepository;
    private readonly ISyncConfigRepository _syncConfigRepository;

    public LineParentBackfillJob(
        IPmisSyncExecutionService executionService,
        ISyncHistoryRepository syncHistoryRepository,
        ISyncConfigRepository syncConfigRepository)
    {
        _executionService = executionService;
        _syncHistoryRepository = syncHistoryRepository;
        _syncConfigRepository = syncConfigRepository;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var (warnings, errorMessage) = await _executionService.BackfillLineParentsAsync();

            // Không có gì đáng chú ý — không tạo dòng SYNC_HISTORY rác mỗi 5 phút.
            if (warnings <= 0 && errorMessage == null) return;

            Log.Warning("LineParentBackfillJob: {ErrorMessage}", errorMessage);

            // Ghi lại 1 dòng SYNC_HISTORY (Cảnh báo) để admin vẫn thấy được qua UI "Lịch sử đồng bộ" —
            // trước khi tách job này ra riêng, cảnh báo "còn N nhánh chưa xác định được cha" hiện ở đây
            // (nằm trong SYNC_HISTORY của lượt sync Đường dây); tách job làm mất tín hiệu đó nếu không bù
            // lại. Total/Success/Failed = 0 vì job này không lấy dữ liệu từ PMIS, chỉ tự sửa nội bộ —
            // CreatedBy giúp phân biệt với 1 lượt sync PMIS thật.
            var syncConfig = await _syncConfigRepository.GetByObjectTypeAsync(SyncObjectType.TransmissionLine);
            var historyId = await _syncHistoryRepository.CreateAsync(new SyncHistory
            {
                SyncConfigId = syncConfig?.Id ?? string.Empty,
                ObjectType = SyncObjectType.TransmissionLine,
                SyncType = SyncType.Auto,
                StartTime = DateTime.UtcNow,
                Status = SyncHistoryStatus.Running,
                CreatedBy = "PMIS_SYNC (LineParentBackfillJob)"
            });
            await _syncHistoryRepository.CompleteAsync(historyId, SyncHistoryStatus.Warning, 0, 0, 0, errorMessage);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "LineParentBackfillJob: lỗi khi tự khớp lại cha/cấp điện áp cho Đường dây.");
        }
    }
}
