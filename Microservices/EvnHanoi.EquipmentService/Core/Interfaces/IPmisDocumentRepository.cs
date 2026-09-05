using EvnHanoi.EquipmentService.Core.DTOs;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

/// <summary>Lưu tài liệu đính kèm đồng bộ từ PMIS (API 8/9) — xem Migration0055_CreatePmisDocumentTable.</summary>
public interface IPmisDocumentRepository
{
    /// <summary>Dùng để bỏ qua tải lại file nếu tài liệu đã đồng bộ trước đó (idempotent theo MaTaiLieu).</summary>
    Task<bool> ExistsByCodeAsync(string pmisDocumentCode);

    /// <summary>Dò Id thật của Trạm/Đường dây (INFRASTRUCTURE) hoặc Thiết bị (EQUIPMENTS) theo PmisCode,
    /// tuỳ ownerType — dùng để gán OwnerId trước khi lưu tài liệu.</summary>
    Task<Guid?> ResolveOwnerIdAsync(string ownerType, string ownerPmisCode);

    Task InsertAsync(UpsertPmisDocumentRequest item, Guid ownerId, string? objectKey, long? fileSize);
}
