using EvnHanoi.EquipmentService.Core.DTOs;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

/// <summary>Quản lý bảng PMIS_UNIT_CODE_MAPPING (ánh xạ mã đơn vị PMIS ↔ UnitId thật) — trước đây chỉ
/// seed được qua Migration0051 (12 đơn vị theo đúng quy luật đã biết), chưa có cách thêm đơn vị mới
/// (vd. "PD6800") ngoài việc dev tự chạy SQL tay. Xem PmisUnitCodeMappingController.</summary>
public interface IPmisUnitCodeMappingRepository
{
    Task<IEnumerable<PmisUnitCodeMappingDto>> GetAllAsync();

    Task<CreatePmisUnitCodeMappingResult> CreateAsync(CreatePmisUnitCodeMappingRequest request, string? createdBy);

    /// <summary>Xoá mềm 1 ánh xạ — an toàn để thêm lại đúng mã đơn vị PMIS đó sau này nhờ Migration0058
    /// đã đổi UQ_PMIS_UNIT_CODE_MAPPING_CODE thành unique index chỉ tính dòng IsDeleted = 0.</summary>
    Task<bool> DeleteAsync(Guid id);
}
