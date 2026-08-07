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
    Task<Equipment?> GetByCodeAsync(string code, Guid? infrastructureId);
    Task<EquipmentDto?> GetDtoByIdAsync(Guid id);
    Task<(IEnumerable<EquipmentExternalDto> Items, int TotalCount)> GetExternalListAsync(PmisEquipmentListRequestDto filter);
    Task<(IEnumerable<EquipmentDetailListDto> Items, int TotalCount)> GetExternalListWithItemsAsync(PmisEquipmentListRequestDto filter);
    Task<IEnumerable<Equipment>> GetAllAsync(IEnumerable<long>? unitIds = null);
    Task<(IEnumerable<EquipmentDto> Items, int TotalCount)> GetPagedAsync(
        int page, 
        int pageSize, 
        string? keyword,
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
    Task<bool> CloneForInfrastructureTransferAsync(Equipment sourceEquipment, Equipment replacementEquipment);
    Task<Equipment?> GetDetailTransferTargetAsync(Equipment sourceEquipment);
    Task<bool> CloneDossiersAndDocumentsForDetailTransferAsync(Equipment sourceEquipment, Equipment replacementEquipment);
    Task<bool> UpdateAsync(Equipment equipment);
    Task<bool> ConfirmAsync(Guid id, string modifiedBy);
    Task<bool> UpdateAttributesAsync(Guid equipmentId, IEnumerable<AttributeValue> attributes);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<AttributeValue>> GetAttributesAsync(Guid equipmentId);
    
    // Lookups
    Task<IEnumerable<OrganizationDto>> GetOrganizationUnitsHierarchicalAsync(long? startUnitId);
    Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync(IEnumerable<long>? authorizedUnitIds = null);
    Task<IEnumerable<EquipmentTypeDto>> GetEquipmentTypesLookupAsync();
    Task<(IEnumerable<EquipmentLookupItemDto> Items, int TotalCount)> GetLookupPagedAsync(
        EquipmentLookupFilterDto filter,
        IEnumerable<long>? authorizedUnitIds);
    Task<int> CountByInfrastructureIdAsync(Guid infrastructureId);
}
