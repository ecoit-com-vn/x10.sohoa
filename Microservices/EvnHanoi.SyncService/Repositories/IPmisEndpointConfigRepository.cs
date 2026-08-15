using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Repositories;

public interface IPmisEndpointConfigRepository
{
    Task<IEnumerable<PmisApiEndpointConfigListItemDto>> GetAllAsync();
    Task<PmisApiEndpointConfig?> GetByApiCodeAsync(string apiCode);
    Task<bool> UpdateAsync(string apiCode, UpdatePmisApiEndpointConfigRequest request, string? modifiedBy);
    Task<IEnumerable<PmisApiEndpointHeader>> GetHeadersAsync(string endpointConfigId);
    Task ReplaceHeadersAsync(string endpointConfigId, IReadOnlyCollection<PmisApiEndpointHeader> headers, string? modifiedBy);
}
