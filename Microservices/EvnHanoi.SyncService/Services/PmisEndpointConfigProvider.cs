using EvnHanoi.SyncService.Models;
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
                // Chặn CẢ 2 đầu ngay tại đây — nơi DUY NHẤT mọi caller (PmisScheduledSyncJob,
                // PmisSyncExecutionService, PmisLookupController) đọc PageSize để dùng — thay vì chỉ chặn
                // ở PmisEndpointConfigController.Update (chặn lúc LƯU không bảo vệ được nếu giá trị xấu
                // lọt vào DB bằng đường khác: sửa tay SQL, migration seed sau này...). <=0 → mặc định 100
                // (an toàn tuyệt đối: PageSize=0 sẽ khiến skip không bao giờ tăng, vòng lặp phân trang ở
                // PmisScheduledSyncJob/PmisSyncExecutionService chạy vô hạn). >MaxPageSize → hạ về đúng
                // trần, tránh 1 trang tự vượt xa giới hạn an toàn tổng (PmisPaging.MaxTotalRecordsPerRun).
                PageSize = config.PageSize is > 0
                    ? Math.Min(config.PageSize.Value, PmisPaging.MaxPageSize)
                    : PmisPaging.DefaultPageSize,
                Headers = resolvedHeaders
            };
        });
    }

    public void Invalidate(string apiCode) => _cache.Remove(CacheKey(apiCode));

    private static string CacheKey(string apiCode) => $"pmis:endpoint-config:{apiCode}";
}
