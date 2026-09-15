using EvnHanoi.EquipmentService.Core.DTOs;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

/// <summary>Lưu tài liệu đính kèm đồng bộ từ PMIS (API 8/9) — xem Migration0055_CreatePmisDocumentTable.</summary>
public interface IPmisDocumentRepository
{
    /// <summary>Tra theo mã tài liệu — null nếu chưa từng lưu. Nếu có, <see cref="PmisDocumentLookup.ObjectKey"/>
    /// null nghĩa là lần trước lưu được metadata nhưng chưa tải được file (caller nên thử tải lại và
    /// UpdateFileAsync, KHÔNG InsertAsync lại vì PmisDocumentCode đã UNIQUE).</summary>
    Task<PmisDocumentLookup?> GetByCodeAsync(string pmisDocumentCode);

    /// <summary>Dò Id thật của Trạm/Đường dây (INFRASTRUCTURE) hoặc Thiết bị (EQUIPMENTS) theo PmisCode,
    /// tuỳ ownerType — dùng để gán OwnerId trước khi lưu tài liệu.</summary>
    Task<Guid?> ResolveOwnerIdAsync(string ownerType, string ownerPmisCode);

    Task InsertAsync(UpsertPmisDocumentRequest item, Guid ownerId, string? objectKey, long? fileSize);

    /// <summary>Cập nhật ObjectKey/FileSize cho 1 dòng đã có nhưng trước đó chưa tải được file.</summary>
    Task UpdateFileAsync(string id, string objectKey, long fileSize, string? syncHistoryId);

    /// <summary>Đọc đầy đủ 1 dòng theo Id thật (khác GetByCodeAsync — tra theo mã PMIS, chỉ trả Id/ObjectKey)
    /// — dùng cho màn "Kho tài liệu PMIS" (xem chi tiết/tải về) và "Chọn từ kho PMIS" (kiểm tra quyền + copy vào hồ sơ).</summary>
    Task<PmisDocumentDetail?> GetByIdAsync(Guid id);

    /// <summary>Cấp gốc của cây "Kho tài liệu PMIS": chỉ danh sách Đơn vị (công ty) có ít nhất 1
    /// Trạm/Đường dây (hoặc thiết bị con) đã có tài liệu PMIS — KHÔNG tải kèm Trạm/Đường dây/Thiết bị,
    /// dùng để load lười (lazy) khi người dùng mở node gốc, tránh quét toàn bộ INFRASTRUCTURE/EQUIPMENTS
    /// như GetCatalogTreeAsync cũ.</summary>
    Task<IReadOnlyList<PmisDocumentCatalogNodeDto>> GetCatalogUnitsAsync();

    /// <summary>Trạm/Đường dây + Thiết bị con của đúng 1 Đơn vị (unitNodeId dạng "unit_{id}" hoặc
    /// "unit_unassigned") — dùng để load lười khi người dùng click mở 1 công ty trong cây.</summary>
    Task<IReadOnlyList<PmisDocumentCatalogNodeDto>> GetCatalogUnitChildrenAsync(string unitNodeId);

    /// <summary>Danh sách tài liệu của đúng 1 node lá (Trạm/Đường dây hoặc Thiết bị) trong cây trên.</summary>
    Task<(IEnumerable<PmisDocumentDetail> Items, int TotalCount)> GetByOwnerAsync(
        string ownerType, Guid ownerId, string? keyword, int page, int pageSize);

    /// <summary>Nút "Upload tài liệu" thủ công khi đồng bộ tự động lỗi — tự sinh PmisDocumentCode dạng
    /// "MANUAL_{guid}" (không trùng mã PMIS thật, vẫn thoả UNIQUE) để phân biệt CreatedBy khác 'PMIS_SYNC'.</summary>
    Task<Guid> InsertManualAsync(string ownerType, Guid ownerId, string documentName, string? documentType, string objectKey, long fileSize, string uploadedBy);
}
