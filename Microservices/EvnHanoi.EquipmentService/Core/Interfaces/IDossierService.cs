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
    Task<(IEnumerable<EquipmentLookupItemDto> Items, int TotalCount)> GetEquipmentLookupAsync(
        EquipmentLookupFilterDto filter,
        bool isAdmin,
        long? userUnitId,
        IReadOnlyList<long>? fallbackUnitIds);

    // CRUD cơ bản
    Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(DossierFilterDto filter);
    Task<DossierDetailDto?> GetDetailByIdAsync(Guid id);
    Task<Guid> CreateAsync(DossierCreateDto dto, string userId, string userName, string userFullName);
    Task<bool> UpdateAsync(Guid id, DossierUpdateDto dto, string userId);
    Task<bool> DeleteAsync(Guid id, string userId);
    Task<bool> CompleteInputAsync(Guid id, string userId);

    // Form data + versioning
    Task<DossierDetailDto?> SaveFormDataAsync(Guid id, DossierSaveFormDataDto dto, string userId);
    Task<IEnumerable<DossierVersionDto>> GetVersionsAsync(Guid id);

    // Equipment management
    Task<IEnumerable<DossierEquipmentDto>> GetEquipmentsAsync(Guid id);
    Task<bool> AddEquipmentAsync(Guid id, Guid equipmentId);
    Task<bool> RemoveEquipmentAsync(Guid id, Guid equipmentId);

    // Workflow đã chuyển sang WorkflowService (DossierWorkflowController).
    // ES chỉ còn nhận đồng bộ trạng thái qua API nội bộ (không expose ra Gateway).
    Task UpdateWorkflowStateInternalAsync(Guid id, UpdateInternalWorkflowStateDto dto);

    // Document tab helpers
    Task RecordDocumentListChangeAsync(Guid dossierId, string changeNote, string userId);
    Task EnsureCanEditFormDataAsync(Guid dossierId);
}
