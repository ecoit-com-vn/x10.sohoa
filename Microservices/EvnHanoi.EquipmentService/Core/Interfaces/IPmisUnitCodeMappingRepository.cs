using EvnHanoi.EquipmentService.Core.DTOs;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

/// <summary>Quản lý bảng PMIS_UNIT_CODE_MAPPING (ánh xạ mã đơn vị PMIS ↔ UnitId thật) — trước đây chỉ
/// seed được qua Migration0051 (12 đơn vị theo đúng quy luật đã biết), chưa có cách thêm đơn vị mới
/// (vd. "PD6800") ngoài việc dev tự chạy SQL tay. Xem PmisUnitCodeMappingController.</summary>
public interface IPmisUnitCodeMappingRepository
{
    Task<IEnumerable<PmisUnitCodeMappingDto>> GetAllAsync();

    Task<CreatePmisUnitCodeMappingResult> CreateAsync(CreatePmisUnitCodeMappingRequest request, string? createdBy);

    // Cố ý CHƯA có Delete: UQ_PMIS_UNIT_CODE_MAPPING_CODE (Migration0051) vẫn là UNIQUE constraint thường
    // (tính cả dòng đã xoá mềm) — thêm xoá mà không sửa lại thành unique index có điều kiện IsDeleted=0
    // (như PMIS_EQUIPMENT_TYPE_MAPPING đã phải vá ở Migration0054) sẽ chặn nhầm việc thêm lại đúng mã đơn
    // vị PMIS đã từng bị xoá. Xem ghi chú ngay trong Migration0054_FixPmisEquipmentTypeMappingUnique.
}
