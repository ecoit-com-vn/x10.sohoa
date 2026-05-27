using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDossierRepository
{
    Task<Dossier?> GetByIdAsync(Guid id);
    Task<IEnumerable<Dossier>> GetAllAsync();
    Task<bool> CreateAsync(Dossier dossier);
    Task<bool> UpdateAsync(Dossier dossier); // Will implement Optimistic Locking
    Task<bool> DeleteAsync(Guid id);
    Task<bool> CreateVersionAsync(DossierVersion version);
    Task<IEnumerable<DossierVersion>> GetVersionsAsync(Guid dossierId);
}
