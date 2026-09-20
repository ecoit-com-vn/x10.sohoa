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

    /// <summary>Thử khớp lại cha cho các Đường dây ĐÃ đồng bộ từ trước nhưng vẫn chưa xác định được cha
    /// (tên có "/" nhưng ParentInfrastructureId còn null) — gọi 1 lần vào CUỐI mỗi lượt đồng bộ Đường dây
    /// (sau khi toàn bộ các trang đã xử lý xong, đường trục nào mới xuất hiện trong CHÍNH lượt này cũng đã
    /// chắc chắn tồn tại). Trả về (số cần cộng vào Warnings, thông báo lỗi tóm tắt nếu có để thêm vào danh
    /// sách Errors — null nếu không còn dòng nào chưa khớp được) — caller chỉ cần cộng/thêm thẳng, không
    /// phải tự lặp lại logic log/format message.</summary>
    Task<(int WarningDelta, string? ErrorMessage)> BackfillLineParentsAsync();
}
