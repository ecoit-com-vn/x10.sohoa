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
    private readonly SemaphoreSlim _processingLock = new(1, 1);
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
                await _processingLock.WaitAsync(stoppingToken);
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

                        var indexed = await IndexDossierAsync(evt.DossierId, stoppingToken);
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
                finally
                {
                    _processingLock.Release();
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
        var enrichmentRepository = scope.ServiceProvider.GetRequiredService<IDossierEnrichmentRepository>();
        var documentBuilder = scope.ServiceProvider.GetRequiredService<IDossierDocumentBuilder>();

        var data = await enrichmentRepository.GetByIdAsync(dossierId);
        if (data is null)
        {
            _logger.LogWarning("Dossier {DossierId} not found in Oracle. Skipping index.", dossierId);
            return true;
        }

        var bhsCatalogs = await enrichmentRepository.GetBhsCatalogDefinitionsAsync();
        var equipments = await enrichmentRepository.GetEquipmentsAsync(dossierId);
        var document = documentBuilder.Build(data, bhsCatalogs, equipments);

        var response = await _elasticClient.IndexAsync(
            document,
            idx => idx.Index(DossierMessaging.IndexName).Id(document.Id),
            cancellationToken);

        if (!response.IsValidResponse &&
            response.ElasticsearchServerError?.Error?.Type == "index_not_found_exception")
        {
            _logger.LogWarning("Index {IndexName} missing, creating now...", DossierMessaging.IndexName);
            await DossierIndexSetup.EnsureIndexExistsAsync(_elasticClient, _logger, cancellationToken);
            response = await _elasticClient.IndexAsync(
                document,
                idx => idx.Index(DossierMessaging.IndexName).Id(document.Id),
                cancellationToken);
        }

        if (!response.IsValidResponse)
        {
            _logger.LogError(
                "Failed to index dossier {DossierId}: {Error}",
                dossierId,
                response.DebugInformation);
            return false;
        }

        _logger.LogInformation(
            "Indexed dossier {DossierId} to {IndexName} (isDeleted={IsDeleted}).",
            dossierId,
            DossierMessaging.IndexName,
            document.IsDeleted);
        return true;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken: cancellationToken);
        _processingLock.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
