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
    Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncInfrastructureAsync(int infraTypeId, string syncHistoryId, IReadOnlyList<JsonElement> rawItems, bool syncDocuments = true);
    Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncEquipmentAsync(string syncHistoryId, IReadOnlyList<JsonElement> rawItems, string? parentPmisCodeFallback = null);

    /// <summary>Đồng bộ tài liệu đính kèm cho ĐÚNG 1 Trạm/Đường dây theo mã PMIS — dùng bởi
    /// PmisScheduledSyncJob.SyncDocumentsRotatingAsync (pass riêng, có rotation qua
    /// SyncConfig.DocumentSyncCursor). Tự tôn trọng <see cref="DocumentSyncBudgetExhausted"/> giống hệt
    /// nhánh đồng bộ tài liệu lồng trong SyncInfrastructureAsync trước đây.</summary>
    Task<(int Warnings, List<SyncHistoryDetail> Details)> SyncDocumentsForInfrastructureOwnerAsync(string ownerPmisCode, int infraTypeId, string syncHistoryId);

    /// <summary>true nếu lượt chạy hiện tại (Scoped: 1 instance/lượt job) đã dùng hết ngân sách gọi PMIS
    /// thật cho Thiết bị (ChiTietThietBi/QR) — PmisScheduledSyncJob.RunEquipmentAsync dùng để biết từ cha
    /// nào trở đi cần ưu tiên ở lượt sau (xem SyncConfig.SyncCursor).</summary>
    bool EquipmentDetailBudgetExhausted { get; }

    /// <summary>true nếu lượt chạy hiện tại đã dừng đồng bộ tài liệu đính kèm vì chạm trần số owner/lượt —
    /// PmisScheduledSyncJob coi ngang hàng với chạm giới hạn an toàn chính (dừng phân trang, lưu cursor).</summary>
    bool DocumentSyncBudgetExhausted { get; }
}
