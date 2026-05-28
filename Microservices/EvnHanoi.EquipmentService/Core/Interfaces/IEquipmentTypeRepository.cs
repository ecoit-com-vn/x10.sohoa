using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEquipmentTypeRepository
{
    Task<EquipmentType?> GetByIdAsync(Guid id);
    Task<IEnumerable<EquipmentType>> GetAllAsync();
    Task<bool> CreateAsync(EquipmentType type);
    Task<bool> UpdateAsync(EquipmentType type);
    Task<bool> DeleteAsync(Guid id);
    
    Task<IEnumerable<AttributeDefinition>> GetAttributeDefinitionsAsync(Guid equipmentTypeId);
    Task<bool> AddAttributeDefinitionAsync(AttributeDefinition attributeDefinition);
}
