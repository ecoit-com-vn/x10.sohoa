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
}
