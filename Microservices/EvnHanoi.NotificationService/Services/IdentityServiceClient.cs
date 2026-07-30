using System.Text.Json;

namespace EvnHanoi.NotificationService.Services;

public interface IIdentityServiceClient
{
    Task<IReadOnlyList<string>> GetActiveUserIdsByUnitAsync(long unitId, CancellationToken cancellationToken = default);
}

/// <summary>Gọi API nội bộ IdentityService (internal/v1/users/by-unit/{unitId}) để phân giải người nhận theo đơn vị.</summary>
public class IdentityServiceClient : IIdentityServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IdentityServiceClient> _logger;

    public IdentityServiceClient(IHttpClientFactory httpClientFactory, ILogger<IdentityServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GetActiveUserIdsByUnitAsync(long unitId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var response = await client.GetAsync($"internal/v1/users/by-unit/{unitId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Không lấy được danh sách tài khoản của đơn vị {UnitId} từ IdentityService: {StatusCode}",
                    unitId,
                    response.StatusCode);
                return Array.Empty<string>();
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<ByUnitResponse>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            return result?.UserIds ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi IdentityService để lấy tài khoản theo đơn vị {UnitId}.", unitId);
            return Array.Empty<string>();
        }
    }

    private class ByUnitResponse
    {
        public List<string> UserIds { get; set; } = new();
    }
}
