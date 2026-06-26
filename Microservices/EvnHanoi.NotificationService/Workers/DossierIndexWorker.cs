using Elastic.Clients.Elasticsearch;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Repositories;
using EvnHanoi.NotificationService.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace EvnHanoi.NotificationService.Workers;

public class DossierIndexWorker : BackgroundService
{
    private readonly ILogger<DossierIndexWorker> _logger;
    private readonly ElasticsearchClient _elasticClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnection _connection;
    private IChannel? _channel;

    public DossierIndexWorker(
        ILogger<DossierIndexWorker> logger,
        ElasticsearchClient elasticClient,
        IServiceScopeFactory scopeFactory,
        IConnection connection)
    {
        _logger = logger;
        _elasticClient = elasticClient;
        _scopeFactory = scopeFactory;
        _connection = connection;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await DossierIndexSetup.EnsureIndexExistsAsync(_elasticClient, _logger, stoppingToken);

            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await _channel.QueueDeclareAsync(
                queue: DossierMessaging.IndexQueue,
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
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    _logger.LogInformation("Received dossier index message: {Message}", message);

                    try
                    {
                        var evt = JsonSerializer.Deserialize<DossierChangedEvent>(
                            message,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (evt is null || string.IsNullOrWhiteSpace(evt.DossierId))
                        {
                            _logger.LogWarning("Invalid dossier changed event. Dropping message.");
                            await _channel!.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                            return;
                        }

                        var indexed = evt.Action.Equals(DossierChangedActions.Deleted, StringComparison.OrdinalIgnoreCase)
                            ? await DeleteDossierAsync(evt.DossierId, stoppingToken)
                            : await IndexDossierAsync(evt.DossierId, stoppingToken);
                        if (indexed)
                            await _channel!.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                        else
                            await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing dossier index message for delivery {Tag}", ea.DeliveryTag);
                        try
                        {
                            await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, CancellationToken.None);
                        }
                        catch (Exception nackEx)
                        {
                            _logger.LogError(nackEx, "Failed to NACK dossier index message.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in dossier index consumer.");
                }
            };

            await _channel.BasicConsumeAsync(
                queue: DossierMessaging.IndexQueue,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(1000, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect or consume dossier index queue.");
        }
    }

    internal async Task<bool> IndexDossierAsync(string dossierId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var indexer = scope.ServiceProvider.GetRequiredService<IDossierIndexer>();
        return await indexer.IndexByIdAsync(dossierId, cancellationToken);
    }

    internal async Task<bool> DeleteDossierAsync(string dossierId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var indexer = scope.ServiceProvider.GetRequiredService<IDossierIndexer>();
        return await indexer.DeleteByIdAsync(dossierId, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken: cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
