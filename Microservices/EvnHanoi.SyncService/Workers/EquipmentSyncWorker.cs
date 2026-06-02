using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Nest;
using Polly;
using Polly.Retry;
using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Workers;

public class EquipmentSyncWorker : BackgroundService
{
    private readonly ILogger<EquipmentSyncWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IConnection _connection;
    private IChannel? _channel;
    private IElasticClient _elasticClient;
    private readonly AsyncRetryPolicy _esRetryPolicy;
    private readonly string _queueName = "equipment_sync_queue";

    public EquipmentSyncWorker(ILogger<EquipmentSyncWorker> logger, IConfiguration configuration, IConnection connection)
    {
        _logger = logger;
        _configuration = configuration;
        _connection = connection;

        // Configure Elasticsearch Client
        var esUrl = _configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
        var settings = new ConnectionSettings(new Uri(esUrl))
            .DefaultIndex("equipment_index");
        _elasticClient = new ElasticClient(settings);

        // Polly for ES
        _esRetryPolicy = Polly.Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning($"ES Push failed. Retry {retryCount} after {timeSpan.TotalSeconds}s. Error: {exception.Message}");
                });
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await InitializeRabbitMQAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    private async Task InitializeRabbitMQAsync(CancellationToken cancellationToken)
    {
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await _channel.QueueDeclareAsync(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
        _logger.LogInformation($"Listening to RabbitMQ queue: {_queueName}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel == null) return;

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var messageString = Encoding.UTF8.GetString(body);
            _logger.LogInformation($"Received message: {messageString}");

            try
            {
                var equipment = JsonSerializer.Deserialize<EquipmentSyncMessage>(messageString);
                if (equipment != null)
                {
                    await SyncToElasticsearchAsync(equipment, stoppingToken);
                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    _logger.LogInformation($"Successfully synced Equipment {equipment.Id} to ES.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from RabbitMQ");
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
    }

    private async Task SyncToElasticsearchAsync(EquipmentSyncMessage equipment, CancellationToken cancellationToken)
    {
        await _esRetryPolicy.ExecuteAsync(async () =>
        {
            var response = await _elasticClient.IndexDocumentAsync(equipment, cancellationToken);
            if (!response.IsValid)
            {
                throw new Exception($"Failed to push to ES: {response.OriginalException?.Message ?? response.ServerError?.Error?.Reason}");
            }
        });
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken: cancellationToken);
        }
        await base.StopAsync(cancellationToken);
    }
}
