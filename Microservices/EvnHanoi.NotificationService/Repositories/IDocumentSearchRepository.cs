using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Repositories;

public interface IDocumentSearchRepository
{
    Task<(IReadOnlyList<DocumentSearchItemDto> Items, int TotalCount)> SearchAsync(DocumentSearchFilterDto filter);
    Task<DocumentEsDocument?> GetByVersionIdAsync(string documentVersionId);
}
