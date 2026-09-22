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

    /// <summary>Các Đường dây ĐÃ tồn tại cần "khớp lại" bởi LineParentBackfillJob (job Quartz riêng chạy
    /// nền định kỳ, KHÔNG chèn vào lượt đồng bộ Đường dây nào) — gồm 2 trường hợp: (1) tên có "/" nhưng
    /// vẫn chưa xác định được cha; (2) đã có cha nhưng GridTypeId còn thiếu (trả kèm ParentId/
    /// ParentGridTypeId để không cần resolve lại theo tên) — xem
    /// PmisSyncExecutionService.BackfillLineParentsAsync.</summary>
    Task<List<LineNameIndexEntry>> GetLinesNeedingBackfillAsync();

    /// <summary>Cập nhật RIÊNG cột ParentInfrastructureId cho các Đường dây trong danh sách — trả về số
    /// dòng cập nhật thành công.</summary>
    Task<int> BackfillLineParentsAsync(List<BackfillLineParentItem> items);

    /// <summary>Tạo 1 Đường dây THẬT cho 1 cấp waypoint trung gian mà PMIS không tự cung cấp bản ghi
    /// riêng — xem PmisSyncExecutionService.ResolveOrCreateParentChainAsync. Trả về null nếu tạo lỗi
    /// (caller tự log/dừng chuỗi, thử lại ở lượt sau).</summary>
    Task<Guid?> CreateSyntheticLineAsync(CreateSyntheticLineRequest request);

    Task<List<UpsertPmisDocumentResult>> UpsertDocumentsAsync(List<UpsertPmisDocumentRequest> items);
}

public class SyncedInfrastructurePmisCode
{
    public string PmisCode { get; set; } = string.Empty;
    public int InfraTypeId { get; set; }
}
