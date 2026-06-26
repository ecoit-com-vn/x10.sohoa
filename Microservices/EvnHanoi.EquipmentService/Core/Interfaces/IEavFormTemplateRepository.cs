using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEavFormTemplateRepository
{
    Task<EavFormTemplate?> GetByIdAsync(Guid id);
    Task<IEnumerable<EavFormTemplate>> GetAllActiveAsync(string? formType = null, bool? isActive = true);
    Task AddAsync(EavFormTemplate template);
    Task UpdateAsync(EavFormTemplate template);
    Task<IEnumerable<EavFormTemplate>> GetVersionsByCodeAsync(string code);
}
