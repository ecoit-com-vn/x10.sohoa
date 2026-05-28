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
    private IConnection? _connection;
    private IModel? _channel;
    private IElasticClient _elasticClient;
    private readonly AsyncRetryPolicy _esRetryPolicy;
    private readonly RetryPolicy _rabbitRetryPolicy;
    private readonly string _queueName = "equipment_sync_queue";

    public EquipmentSyncWorker(ILogger<EquipmentSyncWorker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

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

        // Polly for RabbitMQ connection
        _rabbitRetryPolicy = Polly.Policy
            .Handle<Exception>()
            .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning($"RabbitMQ Connection failed. Retry {retryCount} after {timeSpan.TotalSeconds}s. Error: {exception.Message}");
                });
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        InitializeRabbitMQ();
        return base.StartAsync(cancellationToken);
    }

    private void InitializeRabbitMQ()
    {
        _rabbitRetryPolicy.Execute(() =>
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
                UserName = _configuration["RabbitMQ:Username"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest",
                Port = int.TryParse(_configuration["RabbitMQ:Port"], out var port) ? port : 5672
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
            _channel.QueueDeclare(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _logger.LogInformation($"Listening to RabbitMQ queue: {_queueName}");
        });
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel == null) return Task.CompletedTask;

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
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
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    _logger.LogInformation($"Successfully synced Equipment {equipment.Id} to ES.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from RabbitMQ");
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);
        return Task.CompletedTask;
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

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _connection?.Close();
        return base.StopAsync(cancellationToken);
    }
}
