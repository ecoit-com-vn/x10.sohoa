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
    /// <summary>Lookup biểu mẫu FORM trạng thái Hoàn thành và đang hoạt động.</summary>
    Task<IEnumerable<EavFormTemplate>> GetCompletedActiveFormsAsync();
    Task AddAsync(EavFormTemplate template);
    Task UpdateAsync(EavFormTemplate template);
    Task<IEnumerable<EavFormTemplate>> GetVersionsByCodeAsync(string code);
    Task<EavFormTemplate?> GetByIdAndVersionAsync(Guid id, int version);

    // Version management methods
    Task AddVersionAsync(EavFormTemplateVersion version);
    Task DeactivateVersionsAsync(Guid formTemplateId);
    Task<int> GetMaxVersionAsync(Guid formTemplateId);
    Task DeleteVersionsAsync(Guid formTemplateId);
    Task ApproveVersionAsync(Guid formTemplateId, string status);
    Task ActivateVersionAsync(Guid versionId);
    /// <summary>Khôi phục: đặt phiên bản chỉ định thành IsActive=1, các phiên bản khác = 0.</summary>
    Task<bool> RestoreVersionAsync(Guid formTemplateId, int version);
}
