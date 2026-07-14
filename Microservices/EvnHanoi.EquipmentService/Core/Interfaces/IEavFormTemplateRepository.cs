using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEavFormTemplateRepository
{
    Task<EavFormTemplate?> GetByIdAsync(Guid id);
    Task<EavFormTemplate?> GetActiveByEquipmentTypeIdAsync(Guid equipmentTypeId);
    Task<IEnumerable<EavFormTemplate>> GetAllActiveAsync(string? formType = null, bool? isActive = true);
    Task<IEnumerable<EavFormTemplate>> GetDesignFormsAsync();
    Task<IEnumerable<EavFormTemplate>> GetApprovalFormsAsync();
    Task<IEnumerable<EavFormTemplate>> GetCompletedFormsAsync();
    Task AddAsync(EavFormTemplate template);
    Task UpdateAsync(EavFormTemplate template);
    Task<IEnumerable<EavFormTemplate>> GetVersionsByCodeAsync(string code);

    // Version management methods
    Task AddVersionAsync(EavFormTemplateVersion version);
    Task DeactivateVersionsAsync(Guid formTemplateId);
    Task<int> GetMaxVersionAsync(Guid formTemplateId);
    Task DeleteVersionsAsync(Guid formTemplateId);
    Task ApproveVersionAsync(Guid formTemplateId, string status);
}
