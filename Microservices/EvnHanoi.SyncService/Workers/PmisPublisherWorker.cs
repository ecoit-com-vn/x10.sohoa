using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Workers;

public class PmisPublisherWorker : BackgroundService
{
    private readonly ILogger<PmisPublisherWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AsyncRetryPolicy _httpRetryPolicy;
    private readonly RetryPolicy _rabbitRetryPolicy;
    
    private IConnection? _connection;
    private IModel? _channel;
    private readonly string _queueName = "pmis_push_queue";
    private readonly string _pmisPushApiUrl = "http://pmis.evn.com.vn/api/sync/push";

    public PmisPublisherWorker(ILogger<PmisPublisherWorker> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;

        // Polly Retry Policy cho HTTP Client (Push sang PMIS)
        _httpRetryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning($"PMIS Push HTTP call failed. Retry {retryCount} after {timeSpan.TotalSeconds}s. Error: {exception.Message}");
                });

        // Polly Retry Policy cho RabbitMQ
        _rabbitRetryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning($"RabbitMQ Connection failed in PmisPublisherWorker. Retry {retryCount} after {timeSpan.TotalSeconds}s. Error: {exception.Message}");
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
            _logger.LogInformation($"PmisPublisherWorker initialized RabbitMQ connection. Target queue: {_queueName}");
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
            _logger.LogInformation($"Received message to push to PMIS: {messageString}");

            try
            {
                var payload = JsonSerializer.Deserialize<PmisPushPayload>(messageString);
                if (payload != null)
                {
                    await PushToPmisAsync(payload, stoppingToken);
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    _logger.LogInformation($"Successfully pushed equipment {payload.EquipmentCode} data to PMIS.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PMIS push message from RabbitMQ");
                // Reject and requeue
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);
        return Task.CompletedTask;
    }

    private async Task PushToPmisAsync(PmisPushPayload payload, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("PMIS");
        
        await _httpRetryPolicy.ExecuteAsync(async () =>
        {
            // Giáº£ láº­p Ä‘Ã³ng gÃ³i payload (thÃ´ng sá»‘ ká»¹ thuáº­t Thiáº¿t bá»‹, BiÃªn báº£n xuáº¥t xÆ°á»Ÿng, Dá»¯ liá»‡u CBM)
            var response = await client.PostAsJsonAsync(_pmisPushApiUrl, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
        });
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _connection?.Close();
        return base.StopAsync(cancellationToken);
    }
}
