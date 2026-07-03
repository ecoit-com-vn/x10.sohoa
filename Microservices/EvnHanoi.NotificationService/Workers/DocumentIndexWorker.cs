using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Services;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace EvnHanoi.NotificationService.Workers;

public class DocumentIndexWorker : BackgroundService
{
    private readonly ILogger<DocumentIndexWorker> _logger;
    private readonly ElasticsearchClient _elasticClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnection _connection;
    private IChannel? _channel;

    public DocumentIndexWorker(
        ILogger<DocumentIndexWorker> logger,
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
            await DocumentIndexSetup.EnsureIndexExistsAsync(_elasticClient, _logger, stoppingToken);

            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await DigitizationTopicTopology.EnsureAsync(_channel, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    _logger.LogInformation("Received document text index message: {Message}", message);

                    var evt = JsonSerializer.Deserialize<DocumentTextIndexEvent>(
                        message,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (evt is null || string.IsNullOrWhiteSpace(evt.DocumentVersionId))
                    {
                        _logger.LogWarning("Invalid document text index event. Dropping message.");
                        await _channel!.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                        return;
                    }

                    var success = evt.Action.Equals(DocumentTextIndexActions.Delete, StringComparison.OrdinalIgnoreCase)
                        ? await DeleteAsync(evt.DocumentVersionId, stoppingToken)
                        : await IndexAsync(evt, stoppingToken);

                    if (success)
                        await _channel!.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                    else
                        await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing document text index message.");
                    try
                    {
                        await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, CancellationToken.None);
                    }
                    catch (Exception nackEx)
                    {
                        _logger.LogError(nackEx, "Failed to NACK document text index message.");
                    }
                }
            };

            await _channel.BasicConsumeAsync(
                queue: DocumentTextMessaging.IndexQueue,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation(
                "DocumentIndexWorker listening on queue {QueueName}.",
                DocumentTextMessaging.IndexQueue);

            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(1000, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect or consume document text index queue.");
        }
    }

    private async Task<bool> IndexAsync(DocumentTextIndexEvent evt, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var indexer = scope.ServiceProvider.GetRequiredService<IDocumentIndexer>();
        return await indexer.IndexByVersionIdAsync(
            evt.DocumentVersionId,
            evt.BucketName,
            evt.FilePath,
            evt.TotalPages,
            cancellationToken);
    }

    private async Task<bool> DeleteAsync(string documentVersionId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var indexer = scope.ServiceProvider.GetRequiredService<IDocumentIndexer>();
        return await indexer.DeleteByVersionIdAsync(documentVersionId, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken: cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
