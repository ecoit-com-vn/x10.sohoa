using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Repositories;

public interface IDossierEnrichmentRepository
{
    Task<DossierEnrichmentData?> GetByIdAsync(string dossierId);
    Task<IEnumerable<string>> GetAllIdsAsync();
    Task<IEnumerable<BhsCatalogDefinition>> GetBhsCatalogDefinitionsAsync();
    Task<IEnumerable<DossierEquipmentEnrichment>> GetEquipmentsAsync(string dossierId);
}
