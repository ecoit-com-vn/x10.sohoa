using EvnHanoi.SyncService.Models.Internal;

namespace EvnHanoi.SyncService.Clients;

/// <summary>Gọi API nội bộ (internal/v1/...) của EquipmentService để lưu dữ liệu đã đồng bộ từ PMIS.</summary>
public interface IEquipmentServiceClient
{
    Task<List<UpsertInfrastructureFromPmisResult>> UpsertInfrastructureAsync(List<UpsertInfrastructureFromPmisRequest> items);
    Task<List<UpsertEquipmentFromPmisResult>> UpsertEquipmentAsync(List<UpsertEquipmentFromPmisRequest> items);

    /// <summary>Danh sách Trạm/Đường dây đã có PmisCode — dùng để lặp lấy thiết bị con khi auto-sync Thiết bị.</summary>
    Task<List<SyncedInfrastructurePmisCode>> GetSyncedInfrastructurePmisCodesAsync();

    /// <summary>Tải toàn bộ danh mục Đường dây hiện có (Id/Name/mã đơn vị PMIS) — dùng để tự tìm cha theo
    /// tên trong bộ nhớ, xem PmisSyncExecutionService.ResolveParentLineIdAsync.</summary>
    Task<List<LineNameIndexEntry>> GetLineNameIndexAsync();

    /// <summary>Các Đường dây ĐÃ tồn tại (từ lượt đồng bộ trước) nhưng tên có "/" mà vẫn chưa xác định
    /// được cha — dùng để backfill vào cuối mỗi lượt đồng bộ, xem PmisSyncExecutionService.BackfillLineParentsAsync.</summary>
    Task<List<LineNameIndexEntry>> GetLinesMissingParentAsync();

    /// <summary>Cập nhật RIÊNG cột ParentInfrastructureId cho các Đường dây trong danh sách — trả về số
    /// dòng cập nhật thành công.</summary>
    Task<int> BackfillLineParentsAsync(List<BackfillLineParentItem> items);

    Task<List<UpsertPmisDocumentResult>> UpsertDocumentsAsync(List<UpsertPmisDocumentRequest> items);
}

public class SyncedInfrastructurePmisCode
{
    public string PmisCode { get; set; } = string.Empty;
    public int InfraTypeId { get; set; }
}
