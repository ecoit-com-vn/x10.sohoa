using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Services;

public interface IDossierSearchService
{
    Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(DossierFilterDto filter);
}
