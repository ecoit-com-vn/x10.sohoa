using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using InfrastructureEntity = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDossierRepository
{
    Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync();
    Task<IEnumerable<GridType>> GetGridTypesLookupAsync();
    Task<IEnumerable<DossierType>> GetDossierTypesLookupAsync();

    // Danh sách có phân trang và filter — đã chuyển sang Elasticsearch (DossierSearchRepository).
    [Obsolete("Dùng IDossierSearchRepository qua DossierService.GetPagedAsync.")]
    Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(DossierFilterDto filter);

    // Chi tiết
    Task<DossierDetailDto?> GetDetailByIdAsync(Guid id);
    Task<Dossier?> GetByIdAsync(Guid id);

    // CRUD
    Task<Guid> CreateAsync(Dossier dossier, IEnumerable<Guid> equipmentIds);
    Task<bool> UpdateAsync(Dossier dossier, IEnumerable<Guid> equipmentIds);
    Task<bool> SoftDeleteAsync(Guid id, string modifiedBy);

    // Workflow
    Task<bool> UpdateWorkflowAsync(Guid id, Guid workflowInstanceId, string workflowStatusName, string status, string modifiedBy);

    // Equipments
    Task<IEnumerable<DossierEquipmentDto>> GetEquipmentsAsync(Guid dossierId);
    Task<bool> AddEquipmentAsync(Guid dossierId, Guid equipmentId);
    Task<bool> RemoveEquipmentAsync(Guid dossierId, Guid equipmentId);

    // Versions
    Task<int> CreateVersionAsync(DossierVersion version);
    Task<IEnumerable<DossierVersionDto>> GetVersionsAsync(Guid dossierId);

    // Form data
    Task<bool> UpdateFormDataAsync(Guid id, string formDataJson, int expectedRowVersion, string modifiedBy);
}
