using System.Net.Http.Json;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Notifications;

public class HttpDigitizationProgressNotifier : IDigitizationProgressNotifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpDigitizationProgressNotifier> _logger;

    public HttpDigitizationProgressNotifier(
        IHttpClientFactory httpClientFactory,
        ILogger<HttpDigitizationProgressNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task NotifyAsync(DigitizationProgressPushDto payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");
            var response = await client.PostAsJsonAsync(
                "/api/v1/notifications/digitization-progress",
                payload,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Pushed digitization progress OK: dossier {DossierId}, version {VersionId}, {Progress}%",
                    payload.DossierId,
                    payload.DocumentVersionId,
                    payload.Progress);
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Push digitization progress failed: {StatusCode} version {VersionId}, body: {Body}",
                response.StatusCode,
                payload.DocumentVersionId,
                body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Không push được SignalR progress cho version {VersionId}",
                payload.DocumentVersionId);
        }
    }
}
