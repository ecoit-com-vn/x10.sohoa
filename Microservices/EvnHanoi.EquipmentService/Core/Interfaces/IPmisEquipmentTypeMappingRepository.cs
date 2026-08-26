using EvnHanoi.EquipmentService.Core.DTOs;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IPmisEquipmentTypeMappingRepository
{
    Task<IEnumerable<PmisEquipmentTypeMappingDto>> GetAllAsync();

    Task<string> CreateAsync(SavePmisEquipmentTypeMappingRequest request, string? createdBy);

    /// <summary>false khi không tìm thấy dòng khớp cả Id lẫn RowVersion (bị người khác sửa trước).</summary>
    Task<bool> UpdateAsync(string id, SavePmisEquipmentTypeMappingRequest request, string? modifiedBy);

    Task<bool> DeleteAsync(string id, string? modifiedBy);
}
