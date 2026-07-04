using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Repositories;

public interface IDocumentEnrichmentRepository
{
    Task<DocumentEnrichmentData?> GetByVersionIdAsync(string documentVersionId);
    Task<IEnumerable<string>> GetEquipmentNamesByDossierIdAsync(string dossierId);
    Task<IEnumerable<string>> GetIndexableVersionIdsAsync();
    Task<IEnumerable<string>> GetPublishedIndexableVersionIdsAsync();
}
