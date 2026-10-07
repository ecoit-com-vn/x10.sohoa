using System.Text.Json;
using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Services;

/// <summary>
/// Logic upsert dùng chung giữa đồng bộ thủ công (PmisManualSyncController) và đồng bộ tự động
/// (PmisScheduledSyncJob) — nhận danh sách bản ghi PMIS thô (JSON), gọi API nội bộ EquipmentService
/// để lưu, ghi SYNC_HISTORY_DETAIL cho từng bản ghi.
/// </summary>
public interface IPmisSyncExecutionService
{
    /// <summary><paramref name="syncDocuments"/> = false (dùng bởi PmisScheduledSyncJob/luồng AUTO): BỎ
    /// QUA bước đồng bộ tài liệu đính kèm lồng trong hàm này — luồng AUTO tự chạy 1 pass RIÊNG có rotation
    /// SAU khi phân trang chính xong (xem PmisScheduledSyncJob.SyncDocumentsRotatingAsync +
    /// SyncDocumentsForInfrastructureOwnerAsync bên dưới), tránh luôn ưu tiên đúng ~2000 owner đầu danh
    /// sách mỗi lượt (số lượng owner do phân trang PMIS quyết định, không rotate được như Equipment).
    /// Mặc định true (giữ nguyên hành vi cũ) cho luồng Manual (PmisManualSyncController.Save) — số lượng
    /// nhỏ do người dùng tự chọn, không có vấn đề rotation.</summary>
    /// <summary><paramref name="inc"/> != null (chỉ PmisScheduledSyncJob khi bật đồng bộ tăng dần): bỏ qua bản ghi
    /// KHÔNG ĐỔI so với lần đẩy thành công gần nhất (ghi vào <see cref="IncrementalContext.UnchangedCodes"/>) và
    /// điền <see cref="IncrementalContext.ToSave"/> cho các bản ghi đẩy thành công — caller tự ghi PMIS_SYNC_STATE.</summary>
    Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncInfrastructureAsync(int infraTypeId, string syncHistoryId, IReadOnlyList<JsonElement> rawItems, bool syncDocuments = true, IncrementalContext? inc = null);
    /// <param name="syncDocuments">false (luồng AUTO): KHÔNG đồng bộ tài liệu lồng trong từng thiết bị — tài liệu do job DOCUMENT riêng
    /// (PmisDocumentListSyncJob) đảm nhiệm ở cấp Trạm/Đường dây (API trả cả tài liệu thiết bị con). Mặc định true cho luồng Manual.</param>
    Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncEquipmentAsync(string syncHistoryId, IReadOnlyList<JsonElement> rawItems, string? parentPmisCodeFallback = null, IncrementalContext? inc = null, bool syncDocuments = true);

    /// <summary>Đồng bộ tài liệu đính kèm cho ĐÚNG 1 Trạm/Đường dây theo mã PMIS (hoặc 1 khoảng ngày của nó, tiếp tục theo skip) — dùng bởi
    /// PmisDocumentListSyncJob. Xử lý từng trang, có thể dừng mềm giữa chừng (<see cref="DocumentScanOptions.ShouldStop"/>).</summary>
    Task<DocumentOwnerSyncResult> SyncDocumentsForInfrastructureOwnerAsync(string ownerPmisCode, int infraTypeId, string syncHistoryId, DocumentScanOptions? scan = null);

    /// <summary>true nếu lượt chạy hiện tại (Scoped: 1 instance/lượt job) đã dùng hết ngân sách gọi PMIS
    /// thật cho Thiết bị (ChiTietThietBi/QR) — PmisScheduledSyncJob.RunEquipmentAsync dùng để biết từ cha
    /// nào trở đi cần ưu tiên ở lượt sau (xem SyncConfig.SyncCursor).</summary>
    bool EquipmentDetailBudgetExhausted { get; }

    /// <summary>true nếu lượt chạy hiện tại đã dừng đồng bộ tài liệu đính kèm vì chạm trần số owner/lượt —
    /// PmisScheduledSyncJob coi ngang hàng với chạm giới hạn an toàn chính (dừng phân trang, lưu cursor).</summary>
    bool DocumentSyncBudgetExhausted { get; }
}
