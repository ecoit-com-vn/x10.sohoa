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
}
