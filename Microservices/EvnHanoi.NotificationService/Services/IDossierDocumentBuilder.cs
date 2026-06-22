using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Services;

public interface IDossierDocumentBuilder
{
    DossierEsDocument Build(
        DossierEnrichmentData data,
        IEnumerable<BhsCatalogDefinition> bhsCatalogs,
        IEnumerable<DossierEquipmentEnrichment> equipments);
}
