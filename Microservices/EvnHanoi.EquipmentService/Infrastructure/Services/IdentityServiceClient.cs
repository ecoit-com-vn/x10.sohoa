using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Services;

public class IdentityServiceClient : IIdentityServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IdentityServiceClient> _logger;

    public IdentityServiceClient(IHttpClientFactory httpClientFactory, ILogger<IdentityServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> GetCurrentUserSsoNsIdAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            using var response = await client.GetAsync("api/v1/auth/profile", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "IdentityService GetProfile trả về {StatusCode} khi lấy SsoNsId người dùng hiện tại.",
                    (int)response.StatusCode);
                return null;
            }

            var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions, cancellationToken);
            return profile?.SsoNsId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gọi IdentityService để lấy SsoNsId người dùng hiện tại.");
            return null;
        }
    }

    private class UserProfileResponse
    {
        [JsonPropertyName("ssoNsId")]
        public string? SsoNsId { get; set; }
    }
}
