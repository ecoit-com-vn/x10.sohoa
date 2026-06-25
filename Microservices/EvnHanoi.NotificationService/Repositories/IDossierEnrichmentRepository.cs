using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Repositories;

public interface IDossierEnrichmentRepository
{
    Task<DossierEnrichmentData?> GetByIdAsync(string dossierId);
    Task<IEnumerable<string>> GetAllIdsAsync();
    /// <summary>Id hồ sơ đã xóa mềm — dùng purge ES khi startup sync.</summary>
    Task<IEnumerable<string>> GetSoftDeletedIdsAsync();
    Task<IEnumerable<BhsCatalogDefinition>> GetBhsCatalogDefinitionsAsync();
    Task<IEnumerable<DossierEquipmentEnrichment>> GetEquipmentsAsync(string dossierId);
}
