using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEquipmentRepository
{
    Task<Equipment?> GetByIdAsync(Guid id);
    Task<IEnumerable<Equipment>> GetAllAsync(IEnumerable<long>? unitIds = null);
    Task<bool> CreateWithAttributesAsync(Equipment equipment, IEnumerable<AttributeValue> attributes);
    Task<bool> UpdateAsync(Equipment equipment);
    Task<bool> UpdateAttributesAsync(Guid equipmentId, IEnumerable<AttributeValue> attributes);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<AttributeValue>> GetAttributesAsync(Guid equipmentId);
}
