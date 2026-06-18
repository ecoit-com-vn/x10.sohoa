using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

using InfrastructureEntity = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;

public interface IEquipmentRepository
{
    Task<Equipment?> GetByIdAsync(Guid id);
    Task<Equipment?> GetByCodeAsync(string code);
    Task<EquipmentDto?> GetDtoByIdAsync(Guid id);
    Task<IEnumerable<Equipment>> GetAllAsync(IEnumerable<long>? unitIds = null);
    Task<(IEnumerable<EquipmentDto> Items, int TotalCount)> GetPagedAsync(
        int page, 
        int pageSize, 
        string? code, 
        string? name, 
        long? unitId, 
        Guid? infrastructureId, 
        int? gridTypeId, 
        Guid? equipmentTypeId, 
        bool? isActive, 
        IEnumerable<long>? authorizedUnitIds);
    Task<bool> CreateWithAttributesAsync(Equipment equipment, IEnumerable<AttributeValue> attributes);
    Task<bool> CreateAsync(Equipment equipment);
    Task<bool> UpdateAsync(Equipment equipment);
    Task<bool> UpdateAttributesAsync(Guid equipmentId, IEnumerable<AttributeValue> attributes);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<AttributeValue>> GetAttributesAsync(Guid equipmentId);
    
    // Lookups
    Task<IEnumerable<Country>> GetCountriesAsync();
    Task<IEnumerable<OrganizationDto>> GetOrganizationUnitsHierarchicalAsync(long? startUnitId);
    Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync();
    Task<IEnumerable<EquipmentTypeDto>> GetEquipmentTypesLookupAsync();
}
