using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Repositories;

public interface IDossierSearchRepository
{
    Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(
        DossierFilterDto filter,
        IReadOnlyList<BhsCatalogDefinition> bhsCatalogs);
}
