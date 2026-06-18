using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using InfrastructureEntity = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;
using GridTypeEntity = EvnHanoi.EquipmentService.Core.Entities.GridType;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDossierService
{
    //lookup
    Task<IEnumerable<GridTypeEntity>> GetGridTypesLookupAsync();
    Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync();
    Task<IEnumerable<DossierType>> GetDossierTypesLookupAsync();

    // CRUD cơ bản
    Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(DossierFilterDto filter);
    Task<DossierDetailDto?> GetDetailByIdAsync(Guid id);
    Task<Guid> CreateAsync(DossierCreateDto dto, string userId, string userName, string userFullName);
    Task<bool> UpdateAsync(Guid id, DossierUpdateDto dto, string userId);
    Task<bool> DeleteAsync(Guid id, string userId);

    // Form data + versioning
    Task<DossierDetailDto?> SaveFormDataAsync(Guid id, DossierSaveFormDataDto dto, string userId);
    Task<IEnumerable<DossierVersionDto>> GetVersionsAsync(Guid id);

    // Equipment management
    Task<IEnumerable<DossierEquipmentDto>> GetEquipmentsAsync(Guid id);
    Task<bool> AddEquipmentAsync(Guid id, Guid equipmentId);
    Task<bool> RemoveEquipmentAsync(Guid id, Guid equipmentId);

    // Workflow operations (gọi qua HTTP tới WorkflowService)
    Task<DossierDetailDto?> SubmitForApprovalAsync(Guid id, string userId);
    Task<object?> MoveWorkflowAsync(string dossierId, string nextNodeId, string userId, string actionLabel, string? comment, string? nextAssigneeUserId = null);
    Task<object?> GetWorkflowStatusByEntityAsync(string entityId);
    Task<IEnumerable<object>> GetWorkflowHistoryAsync(Guid dossierId);
    Task<object?> GetWorkflowDefinitionAsync(Guid definitionId);
    Task<IEnumerable<object>> GetMyTasksAsync(List<string> userRoles, bool isAdmin, string userId);
}
