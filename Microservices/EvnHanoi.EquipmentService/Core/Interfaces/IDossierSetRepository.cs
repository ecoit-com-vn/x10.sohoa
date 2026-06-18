using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDossierSetRepository
{
    Task<IEnumerable<DossierSetDto>> GetAllAsync(long? unitId = null);
    Task<DossierSet?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(DossierSet dossierSet);
    Task<bool> UpdateAsync(DossierSet dossierSet);
    Task<bool> SoftDeleteAsync(Guid id, string modifiedBy);
}
