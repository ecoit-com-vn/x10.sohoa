using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using InfrastructureEntity = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDossierRepository
{
    Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync(IEnumerable<long>? authorizedUnitIds = null);
    Task<IEnumerable<GridType>> GetGridTypesLookupAsync();
    Task<IEnumerable<DossierType>> GetDossierTypesLookupAsync();
    Task<IEnumerable<DossierGroup>> GetDossierGroupsLookupAsync();
    Task<DossierGroup?> GetDossierGroupByIdAsync(int id);

    // Danh sách có phân trang và filter — đã chuyển sang Elasticsearch (DossierSearchRepository).
    [Obsolete("Dùng IDossierSearchRepository qua DossierService.GetPagedAsync.")]
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
        long? unitId);

    Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupEquipmentTypesAsync(
        DossierByEquipmentFilterDto filter,
        long? unitId);

    Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupEquipmentsAsync(
        DossierByEquipmentFilterDto filter,
        long? unitId);

    Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupDossierTypesAsync(
        DossierByEquipmentFilterDto filter,
        long? unitId);

    Task<IEnumerable<BhsCatalogColumnDto>> GetBhsCatalogColumnsAsync();

    Task<bool> IsPublishedDossierAccessibleAsync(Guid dossierId, long? unitId);

    Task<(IEnumerable<DossierListItemDto> Items, int TotalCount, IEnumerable<BhsCatalogColumnDto> Columns)> GetDossiersByEquipmentAsync(
        Guid equipmentId,
        int page,
        int pageSize);

    // Chi tiết
    Task<DossierDetailDto?> GetDetailByIdAsync(Guid id);
    Task<Dossier?> GetByIdAsync(Guid id);
    Task<int?> GetKindIdAsync(Guid id);

    // CRUD
    Task<Guid> CreateAsync(Dossier dossier, IEnumerable<Guid> equipmentIds);
    Task<bool> UpdateAsync(Dossier dossier, IEnumerable<Guid> equipmentIds);
    Task<bool> SoftDeleteAsync(Guid id, string modifiedBy);
    Task<bool> UpdateStatusAsync(Guid id, int statusId, string modifiedBy);
    Task<bool> UpdatePublishStatusAsync(Guid id, int publishStatusId, string modifiedBy);

    // Workflow
    Task<bool> UpdateWorkflowAsync(Guid id, Guid workflowInstanceId, string workflowStatusName, int statusId, int? publishStatusId, string modifiedBy);
    Task<bool> SaveActiveWorkflowTaskAsync(Guid dossierId, string stepId, string stepName, string assignees, string actionsJson, string modifiedBy);

    // Equipments
    Task<IEnumerable<DossierEquipmentDto>> GetEquipmentsAsync(Guid dossierId);
    Task<bool> AddEquipmentAsync(Guid dossierId, Guid equipmentId);
    Task<bool> RemoveEquipmentAsync(Guid dossierId, Guid equipmentId);

    // Versions
    Task<int> CreateVersionAsync(DossierVersion version);
    Task<IEnumerable<DossierVersionDto>> GetVersionsAsync(Guid dossierId);
    Task<DossierWorkflowStatusDto?> GetWorkflowStatusByEntityAsync(string entityId);

    // Form data
    Task<bool> UpdateFormDataAsync(Guid id, string formDataJson, int expectedRowVersion, string modifiedBy);

    Task<EavFormTemplate?> GetEavFormTemplateAsync(Guid formId);
    Task<EavFormTemplate?> GetEavFormTemplateByDossierIdAsync(Guid dossierId);
}

