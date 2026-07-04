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
    Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync(
        bool isAdmin,
        long? userUnitId,
        IReadOnlyList<long>? fallbackUnitIds);
    Task<IEnumerable<DossierType>> GetDossierTypesLookupAsync();
    Task<(IEnumerable<EquipmentLookupItemDto> Items, int TotalCount)> GetEquipmentLookupAsync(
        EquipmentLookupFilterDto filter,
        bool isAdmin,
        long? userUnitId,
        IReadOnlyList<long>? fallbackUnitIds);

    // CRUD cơ bản
    Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(DossierFilterDto filter);
    Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetCatalogDossiersAsync(
        string? keyword,
        Guid? infrastructureId,
        Guid? dossierTypeId,
        long? unitId,
        int page,
        int pageSize);

    Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupInfrastructuresAsync(
        DossierByEquipmentFilterDto filter,
        bool isAdmin,
        long? userUnitId);

    Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupEquipmentTypesAsync(
        DossierByEquipmentFilterDto filter,
        bool isAdmin,
        long? userUnitId);

    Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupEquipmentsAsync(
        DossierByEquipmentFilterDto filter,
        bool isAdmin,
        long? userUnitId);

    Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupDossierTypesAsync(
        DossierByEquipmentFilterDto filter,
        bool isAdmin,
        long? userUnitId);

    Task<IEnumerable<BhsCatalogColumnDto>> GetBhsCatalogColumnsAsync();

    Task<DossierDetailDto?> GetPublishedDetailByIdAsync(Guid id, bool isAdmin, long? userUnitId);

    Task<bool> IsPublishedDossierAccessibleAsync(Guid id, bool isAdmin, long? userUnitId);

    Task<(IEnumerable<DossierListItemDto> Items, int TotalCount, IEnumerable<BhsCatalogColumnDto> Columns)> GetDossiersByEquipmentAsync(
        Guid equipmentId,
        int page,
        int pageSize);
    Task<DossierDetailDto?> GetDetailByIdAsync(Guid id);
    Task<Guid> CreateAsync(DossierCreateDto dto, string userId, string userName, string userFullName, int kindId = 2);
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
    Task AutoApproveWithoutWorkflowAsync(Guid id);

    // Document tab helpers
    Task RecordDocumentListChangeAsync(Guid dossierId, string changeNote, string userId);
    Task EnsureCanEditFormDataAsync(Guid dossierId);
    Task<bool> UpdatePublishStatusAsync(Guid id, int publishStatusId, string userId);

    Task<EavFormTemplate?> GetFormTemplateForDossierAsync(Guid dossierId, Guid? formId);
}

