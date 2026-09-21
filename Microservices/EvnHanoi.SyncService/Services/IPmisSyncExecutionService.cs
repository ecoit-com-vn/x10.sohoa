using System.Text.Json;

namespace EvnHanoi.SyncService.Services;

/// <summary>
/// Logic upsert dùng chung giữa đồng bộ thủ công (PmisManualSyncController) và đồng bộ tự động
/// (PmisScheduledSyncJob) — nhận danh sách bản ghi PMIS thô (JSON), gọi API nội bộ EquipmentService
/// để lưu, ghi SYNC_HISTORY_DETAIL cho từng bản ghi.
/// </summary>
public interface IPmisSyncExecutionService
{
    Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncInfrastructureAsync(int infraTypeId, string syncHistoryId, IReadOnlyList<JsonElement> rawItems);
    Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncEquipmentAsync(string syncHistoryId, IReadOnlyList<JsonElement> rawItems);

    /// <summary>Thử khớp lại cha/cấp điện áp cho các Đường dây ĐÃ đồng bộ từ trước còn thiếu — chạy ĐỘC LẬP
    /// từ job Quartz riêng (LineParentBackfillJob, tick định kỳ, KHÔNG chèn vào lượt đồng bộ Đường dây
    /// nào). Xử lý 2 trường hợp (xem GetLinesNeedingBackfillAsync): (1) tên có "/" nhưng
    /// ParentInfrastructureId còn null — resolve theo tên; (2) đã có cha nhưng GridTypeId còn null — mượn
    /// thẳng GridTypeId của cha. Trả về (số cần cộng vào Warnings, thông báo lỗi tóm tắt nếu có để thêm vào
    /// danh sách Errors — null nếu không còn dòng nào chưa khớp được) — caller chỉ cần cộng/thêm thẳng,
    /// không phải tự lặp lại logic log/format message.</summary>
    Task<(int WarningDelta, string? ErrorMessage)> BackfillLineParentsAsync();
}
