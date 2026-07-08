using EvnHanoi.Infrastructure.Audit;
using EvnHanoi.NotificationService.Models;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace EvnHanoi.NotificationService.Workers;

public sealed class AuditEventWorker : BackgroundService
{
    private readonly ILogger<AuditEventWorker> _logger;
    private readonly ElasticsearchClient _elasticClient;
    private readonly IConnection _connection;
    private IChannel? _channel;

    public AuditEventWorker(
        ILogger<AuditEventWorker> logger,
        ElasticsearchClient elasticClient,
        IConnection connection)
    {
        _logger = logger;
        _elasticClient = elasticClient;
        _connection = connection;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await _channel.QueueDeclareAsync(
                queue: AuditMessaging.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var message = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var evt = JsonSerializer.Deserialize<AuditEvent>(message, AuditJsonSerializer.Options);

                    if (evt is null || string.IsNullOrWhiteSpace(evt.Id))
                    {
                        _logger.LogWarning("Audit event không hợp lệ, bỏ qua.");
                        await _channel!.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        return;
                    }

                    if (!AuditUserActionGuard.IsUserAction(evt))
                    {
                        _logger.LogDebug(
                            "Bỏ qua audit event không phải thao tác người dùng: {Action} {Path}",
                            evt.Action,
                            evt.RequestPath);
                        await _channel!.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        return;
                    }

                    var indexed = await IndexAuditEventAsync(evt, stoppingToken);
                    if (indexed)
                        await _channel!.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    else
                        await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xử lý audit event.");
                    try
                    {
                        await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                    }
                    catch (Exception nackEx)
                    {
                        _logger.LogError(nackEx, "Không thể NACK audit event.");
                    }
                }
            };

            await _channel.BasicConsumeAsync(
                queue: AuditMessaging.QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("AuditEventWorker đang lắng nghe queue {Queue}.", AuditMessaging.QueueName);

            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(1000, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuditEventWorker không thể khởi động.");
        }
    }

    internal async Task<bool> IndexAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        var indexName = $"{AuditMessaging.IndexPrefix}-{auditEvent.OccurredAt:yyyy.MM.dd}";
        var document = new AuditLogDocument
        {
            Id = auditEvent.Id,
            OccurredAt = auditEvent.OccurredAt,
            ServiceName = auditEvent.ServiceName,
            ActorUserId = auditEvent.ActorUserId,
            UserName = auditEvent.ActorUserName,
            ActorIp = auditEvent.ActorIp,
            Action = auditEvent.Action,
            ResourceType = auditEvent.ResourceType,
            ResourceId = auditEvent.ResourceId,
            ResourceName = auditEvent.ResourceName,
            Details = auditEvent.Details,
            HttpMethod = auditEvent.HttpMethod,
            RequestPath = auditEvent.RequestPath,
            StatusCode = auditEvent.StatusCode,
            CorrelationId = auditEvent.CorrelationId,
            IsDeleted = auditEvent.IsDeleted
        };

        var response = await _elasticClient.IndexAsync(
            document,
            idx => idx.Index(indexName).Id(auditEvent.Id),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogError(
                "Index audit event thất bại: {Reason}",
                response.ElasticsearchServerError?.Error?.Reason);
            return false;
        }

        return true;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken: cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
