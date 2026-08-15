using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Repositories;

public interface ISyncHistoryRepository
{
    Task<string> CreateAsync(SyncHistory history);
    Task CompleteAsync(string id, string status, int totalRecords, int successRecords, int failedRecords, string? errorMessage);
    Task InsertDetailsAsync(IEnumerable<SyncHistoryDetail> details);
    Task<(IEnumerable<SyncHistory> Items, int TotalCount)> GetPagedAsync(string? objectType, int page, int pageSize);
    Task<(IEnumerable<SyncHistoryDetail> Items, int TotalCount)> GetDetailsPagedAsync(string syncHistoryId, int page, int pageSize);
}
