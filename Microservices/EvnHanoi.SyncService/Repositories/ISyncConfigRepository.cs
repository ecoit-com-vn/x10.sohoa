using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Repositories;

public interface ISyncConfigRepository
{
    Task<IEnumerable<SyncConfig>> GetAllAsync();
    Task<SyncConfig?> GetByObjectTypeAsync(string objectType);
    Task<bool> UpdateAsync(string objectType, UpdateSyncConfigRequest request, string? modifiedBy);
    Task UpdateRunResultAsync(string objectType, DateTime lastSyncAt, DateTime? nextSyncAt);
}
