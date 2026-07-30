using System.Text;
using System.Text.Json;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Hubs;
using EvnHanoi.NotificationService.Repositories;
using EvnHanoi.NotificationService.Services;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EvnHanoi.NotificationService.Workers;

/// <summary>
/// Lắng nghe notification.queue (exchange notification.events.exchange) — nhận sự kiện hồ sơ chuyển bước
/// và thiết bị chuyển TBA/chuyển hồ sơ, phân giải người nhận, lưu NOTIFICATIONS/NOTIFICATION_RECIPIENTS,
/// rồi đẩy real-time qua SignalR cho từng người nhận đang online.
/// </summary>
public sealed class NotificationEventsConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationEventsConsumer> _logger;
    private IChannel? _channel;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public NotificationEventsConsumer(
        IConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationEventsConsumer> logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await NotificationTopicTopology.EnsureAsync(_channel, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    await HandleMessageAsync(ea, stoppingToken);
                    await _channel!.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xử lý notification event (routing key {RoutingKey}).", ea.RoutingKey);
                    try
                    {
                        await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                    }
                    catch (Exception nackEx)
                    {
                        _logger.LogError(nackEx, "Không thể NACK notification event.");
                    }
                }
            };

            await _channel.BasicConsumeAsync(
                queue: NotificationTopicTopology.QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("NotificationEventsConsumer đang lắng nghe queue {Queue}.", NotificationTopicTopology.QueueName);

            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(1000, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown bình thường
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationEventsConsumer không thể khởi động.");
        }
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetString(ea.Body.ToArray());

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var identityClient = scope.ServiceProvider.GetRequiredService<IIdentityServiceClient>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();

        switch (ea.RoutingKey)
        {
            case NotificationTopicTopology.DossierMovedRoutingKey:
                await HandleDossierMovedAsync(body, repository, hubContext);
                break;

            case NotificationTopicTopology.EquipmentTbaTransferredRoutingKey:
                await HandleEquipmentTbaTransferredAsync(body, repository, identityClient, hubContext, cancellationToken);
                break;

            case NotificationTopicTopology.EquipmentDossierTransferredRoutingKey:
                await HandleEquipmentDossierTransferredAsync(body, repository, identityClient, hubContext, cancellationToken);
                break;

            default:
                _logger.LogWarning("NotificationEventsConsumer: routing key không xác định {RoutingKey}.", ea.RoutingKey);
                break;
        }
    }

    private async Task HandleDossierMovedAsync(
        string body,
        INotificationRepository repository,
        IHubContext<NotificationHub> hubContext)
    {
        var evt = JsonSerializer.Deserialize<DossierMovedEvent>(body, JsonOptions);
        if (evt == null || evt.RecipientUserIds.Count == 0) return;

        var title = "Hồ sơ mới cần xử lý";
        var stepText = string.IsNullOrWhiteSpace(evt.StepName) ? "" : $" — bước: {evt.StepName}";
        var bodyText = $"Hồ sơ {evt.DossierId} vừa được chuyển đến bạn để xử lý theo luồng{stepText}.";

        var notificationId = await repository.CreateWithRecipientsAsync(
            "DOSSIER_ASSIGNED",
            title,
            bodyText,
            "DOSSIER",
            evt.DossierId,
            evt.ActorUserId,
            evt.RecipientUserIds);

        await PushToRecipientsAsync(hubContext, evt.RecipientUserIds, notificationId, "DOSSIER_ASSIGNED", title, bodyText, "DOSSIER", evt.DossierId);
    }

    private async Task HandleEquipmentTbaTransferredAsync(
        string body,
        INotificationRepository repository,
        IIdentityServiceClient identityClient,
        IHubContext<NotificationHub> hubContext,
        CancellationToken cancellationToken)
    {
        var evt = JsonSerializer.Deserialize<EquipmentTbaTransferredEvent>(body, JsonOptions);
        if (evt == null) return;

        var recipients = new List<string>();
        if (evt.OldUnitId.HasValue)
            recipients.AddRange(await identityClient.GetActiveUserIdsByUnitAsync(evt.OldUnitId.Value, cancellationToken));
        if (evt.NewUnitId.HasValue)
            recipients.AddRange(await identityClient.GetActiveUserIdsByUnitAsync(evt.NewUnitId.Value, cancellationToken));

        recipients = recipients.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (recipients.Count == 0) return;

        var title = "Thiết bị đã chuyển sang TBA mới";
        var bodyText = $"Thiết bị {evt.EquipmentCode ?? evt.EquipmentId.ToString()} đã được chuyển sang trạm biến áp mới.";

        var notificationId = await repository.CreateWithRecipientsAsync(
            "EQUIPMENT_TBA_TRANSFERRED",
            title,
            bodyText,
            "EQUIPMENT",
            evt.EquipmentId.ToString(),
            evt.ActorUserId,
            recipients);

        await PushToRecipientsAsync(hubContext, recipients, notificationId, "EQUIPMENT_TBA_TRANSFERRED", title, bodyText, "EQUIPMENT", evt.EquipmentId.ToString());
    }

    private async Task HandleEquipmentDossierTransferredAsync(
        string body,
        INotificationRepository repository,
        IIdentityServiceClient identityClient,
        IHubContext<NotificationHub> hubContext,
        CancellationToken cancellationToken)
    {
        var evt = JsonSerializer.Deserialize<EquipmentDossierTransferredEvent>(body, JsonOptions);
        if (evt == null || !evt.NewUnitId.HasValue) return;

        var recipients = (await identityClient.GetActiveUserIdsByUnitAsync(evt.NewUnitId.Value, cancellationToken))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (recipients.Count == 0) return;

        var title = "Hồ sơ thiết bị đã được chuyển đến đơn vị bạn";
        var bodyText = $"Hồ sơ của thiết bị {evt.EquipmentCode ?? evt.EquipmentId.ToString()} đã được chuyển đến đơn vị bạn tiếp nhận.";

        var notificationId = await repository.CreateWithRecipientsAsync(
            "EQUIPMENT_DOSSIER_TRANSFERRED",
            title,
            bodyText,
            "EQUIPMENT",
            evt.EquipmentId.ToString(),
            evt.ActorUserId,
            recipients);

        await PushToRecipientsAsync(hubContext, recipients, notificationId, "EQUIPMENT_DOSSIER_TRANSFERRED", title, bodyText, "EQUIPMENT", evt.EquipmentId.ToString());
    }

    /// <summary>
    /// Đẩy qua sự kiện riêng "NotificationCreated" (khác "ReceiveNotification" dùng cho toast broadcast chung)
    /// để không lẫn với các luồng SignalR hiện có (digitization progress, toast quảng bá).
    /// </summary>
    private static async Task PushToRecipientsAsync(
        IHubContext<NotificationHub> hubContext,
        IReadOnlyCollection<string> recipientUserIds,
        string? id,
        string notificationType,
        string title,
        string body,
        string relatedEntityType,
        string relatedEntityId)
    {
        var payload = new { id, notificationType, title, body, relatedEntityType, relatedEntityId, isRead = false };
        foreach (var userId in recipientUserIds)
        {
            await hubContext.Clients.Group(NotificationHub.BuildUserGroup(userId)).SendAsync("NotificationCreated", payload);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken: cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
