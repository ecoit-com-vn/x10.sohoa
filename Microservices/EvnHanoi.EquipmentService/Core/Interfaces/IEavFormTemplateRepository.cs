using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEavFormTemplateRepository
{
    Task<EavFormTemplate?> GetByIdAsync(Guid id);
    Task<IEnumerable<EavFormTemplate>> GetAllActiveAsync();
    Task AddAsync(EavFormTemplate template);
    Task UpdateAsync(EavFormTemplate template);
}
