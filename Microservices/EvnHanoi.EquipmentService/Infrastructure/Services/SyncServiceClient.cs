using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Services;

public class SyncServiceClient : ISyncServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncServiceClient> _logger;

    public SyncServiceClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SyncServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task TriggerSyncNowAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SyncServiceInternal");
            using var request = new HttpRequestMessage(HttpMethod.Post, "internal/v1/sync-config/trigger-now");
            request.Headers.Add("X-Internal-Token", _configuration["Internal:Token"]);

            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "SyncServiceClient.TriggerSyncNowAsync: SyncService trả về {StatusCode} — lượt đồng bộ theo lịch thường vẫn sẽ tự sửa sau.",
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SyncServiceClient.TriggerSyncNowAsync: lỗi gọi SyncService, bỏ qua — lượt đồng bộ theo lịch thường vẫn sẽ tự sửa sau.");
        }
    }
}
