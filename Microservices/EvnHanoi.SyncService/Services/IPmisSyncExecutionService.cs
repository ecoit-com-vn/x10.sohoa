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
    Task<(int Success, int Failed, int Warnings, List<string> Errors)> SyncEquipmentAsync(string syncHistoryId, IReadOnlyList<JsonElement> rawItems, string? parentPmisCodeFallback = null);

    /// <summary>true nếu lượt chạy hiện tại (Scoped: 1 instance/lượt job) đã dùng hết ngân sách gọi PMIS
    /// thật cho Thiết bị (ChiTietThietBi/QR) — PmisScheduledSyncJob.RunEquipmentAsync dùng để biết từ cha
    /// nào trở đi cần ưu tiên ở lượt sau (xem SyncConfig.SyncCursor).</summary>
    bool EquipmentDetailBudgetExhausted { get; }

    /// <summary>true nếu lượt chạy hiện tại đã dừng đồng bộ tài liệu đính kèm vì chạm trần số owner/lượt —
    /// PmisScheduledSyncJob coi ngang hàng với chạm giới hạn an toàn chính (dừng phân trang, lưu cursor).</summary>
    bool DocumentSyncBudgetExhausted { get; }
}
