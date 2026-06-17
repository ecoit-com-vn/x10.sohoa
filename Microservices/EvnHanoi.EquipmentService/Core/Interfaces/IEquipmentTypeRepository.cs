using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEquipmentTypeRepository
{
    Task<EquipmentTypeDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<EquipmentType>> GetAllAsync();
    Task<(IEnumerable<EquipmentTypeDto> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? code, string? name, int? gridTypeId, bool? isActive);
    Task<IEnumerable<GridType>> GetGridTypesAsync();
    Task<bool> CreateAsync(EquipmentType type);
    Task<bool> UpdateAsync(EquipmentType type);
    Task<bool> DeleteAsync(Guid id);
    
    Task<IEnumerable<AttributeDefinition>> GetAttributeDefinitionsAsync(Guid equipmentTypeId);
    Task<bool> AddAttributeDefinitionAsync(AttributeDefinition attributeDefinition);
}
