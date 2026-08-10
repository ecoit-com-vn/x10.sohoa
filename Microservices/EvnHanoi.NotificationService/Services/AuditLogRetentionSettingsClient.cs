using System.Globalization;
using System.Text.Json;

namespace EvnHanoi.NotificationService.Services;

public interface IAuditLogRetentionSettingsClient
{
    Task<int?> GetRetentionDaysAsync(CancellationToken cancellationToken = default);
}

public sealed class AuditLogRetentionSettingsClient : IAuditLogRetentionSettingsClient
{
    private const string AuditLogRetentionDaysKey = "AuditLogRetentionDays";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuditLogRetentionSettingsClient> _logger;

    public AuditLogRetentionSettingsClient(
        IHttpClientFactory httpClientFactory,
        ILogger<AuditLogRetentionSettingsClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<int?> GetRetentionDaysAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            using var response = await client.GetAsync(
                $"internal/v1/system-params/{AuditLogRetentionDaysKey}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Không thể đọc tham số {ParamKey} từ IdentityService. " +
                    "BaseAddress: {BaseAddress}; StatusCode: {StatusCode}; ReasonPhrase: {ReasonPhrase}; ResponseBody: {ResponseBody}",
                    AuditLogRetentionDaysKey,
                    client.BaseAddress,
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    responseBody);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var systemParam = await JsonSerializer.DeserializeAsync<SystemParamResponse>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (!int.TryParse(
                    systemParam?.ParamValue,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var retentionDays) ||
                retentionDays <= 0)
            {
                _logger.LogError(
                    "Tham số {ParamKey} không hợp lệ; job xóa audit log sẽ không chạy.",
                    AuditLogRetentionDaysKey);
                return null;
            }

            return retentionDays;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi khi đọc tham số {ParamKey} từ IdentityService.",
                AuditLogRetentionDaysKey);
            return null;
        }
    }

    private sealed class SystemParamResponse
    {
        public string? ParamValue { get; set; }
    }
}
