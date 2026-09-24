using EvnHanoi.SyncService.Models.Internal;

namespace EvnHanoi.SyncService.Clients;

/// <summary>Gọi API nội bộ (internal/v1/...) của EquipmentService để lưu dữ liệu đã đồng bộ từ PMIS.</summary>
public interface IEquipmentServiceClient
{
    Task<List<UpsertInfrastructureFromPmisResult>> UpsertInfrastructureAsync(List<UpsertInfrastructureFromPmisRequest> items);
    Task<List<UpsertEquipmentFromPmisResult>> UpsertEquipmentAsync(List<UpsertEquipmentFromPmisRequest> items);

    /// <summary>Danh sách Trạm/Đường dây đã có PmisCode — dùng để lặp lấy thiết bị con khi auto-sync Thiết bị.</summary>
    Task<List<SyncedInfrastructurePmisCode>> GetSyncedInfrastructurePmisCodesAsync();

    Task<List<UpsertPmisDocumentResult>> UpsertDocumentsAsync(List<UpsertPmisDocumentRequest> items);

    /// <summary>Số thiết bị bị đánh dấu "Đã chuyển TBA" bởi PMIS_SYNC trong <paramref name="sinceHours"/>
    /// giờ gần đây — xem PmisReconciliationJob.</summary>
    Task<int> GetRecentlyTransferredCountAsync(int sinceHours);
}

public class SyncedInfrastructurePmisCode
{
    public string PmisCode { get; set; } = string.Empty;
    public int InfraTypeId { get; set; }
}
