using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Repositories;

public interface IPmisApiCallLogRepository
{
    Task InsertAsync(PmisApiCallLog log);
    Task<(IEnumerable<PmisApiCallLog> Items, int TotalCount)> GetPagedAsync(string apiCode, int page, int pageSize);
    Task<int> DeleteOlderThanAsync(int retentionDays);
}
