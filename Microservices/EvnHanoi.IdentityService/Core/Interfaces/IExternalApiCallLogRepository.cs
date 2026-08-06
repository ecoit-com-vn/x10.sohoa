using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IExternalApiCallLogRepository
{
    Task<(IEnumerable<ExternalApiCallLog> Items, int TotalCount)> GetByApiKeyIdAsync(long apiKeyId, int page, int pageSize);
    Task<(IEnumerable<ExternalApiCallLog> Items, int TotalCount)> GetAllAsync(ExternalApiCallLogFilter filter, int page, int pageSize);
}
