using EvnHanoi.SyncService.Repositories;
using EvnHanoi.SyncService.Security;
using Microsoft.Extensions.Caching.Memory;

namespace EvnHanoi.SyncService.Services;

public class PmisEndpointConfigProvider : IPmisEndpointConfigProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IPmisEndpointConfigRepository _repository;
    private readonly IPmisHeaderValueProtector _protector;
    private readonly IMemoryCache _cache;

    public PmisEndpointConfigProvider(
        IPmisEndpointConfigRepository repository,
        IPmisHeaderValueProtector protector,
        IMemoryCache cache)
    {
        _repository = repository;
        _protector = protector;
        _cache = cache;
    }

    public Task<ResolvedPmisEndpoint?> GetEndpointAsync(string apiCode)
    {
        return _cache.GetOrCreateAsync(CacheKey(apiCode), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            var config = await _repository.GetByApiCodeAsync(apiCode);
            if (config == null || !config.IsActive || string.IsNullOrWhiteSpace(config.Url))
                return null;

            var headers = await _repository.GetHeadersAsync(config.Id);
            var resolvedHeaders = headers.ToDictionary(
                h => h.HeaderKey,
                h => h.IsSecret ? (_protector.Unprotect(h.HeaderValue) ?? string.Empty) : (h.HeaderValue ?? string.Empty));

            return new ResolvedPmisEndpoint
            {
                ApiCode = config.ApiCode,
                DisplayName = config.DisplayName,
                Url = config.Url!,
                HttpMethod = config.HttpMethod,
                TimeoutSeconds = config.TimeoutSeconds,
                Headers = resolvedHeaders
            };
        });
    }

    public void Invalidate(string apiCode) => _cache.Remove(CacheKey(apiCode));

    private static string CacheKey(string apiCode) => $"pmis:endpoint-config:{apiCode}";
}
