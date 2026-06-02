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
    private readonly IConnection _connection;
    private IChannel? _channel;
    private readonly string _queueName = "pmis_push_queue";
    private readonly string _pmisPushApiUrl = "http://pmis.evn.com.vn/api/sync/push";

    public PmisPublisherWorker(ILogger<PmisPublisherWorker> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory, IConnection connection)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _connection = connection;

        // Polly Retry Policy cho HTTP Client (Push sang PMIS)
        _httpRetryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning($"PMIS Push HTTP call failed. Retry {retryCount} after {timeSpan.TotalSeconds}s. Error: {exception.Message}");
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
        _logger.LogInformation($"PmisPublisherWorker initialized RabbitMQ channel. Target queue: {_queueName}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel == null) return;

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
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
                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    _logger.LogInformation($"Successfully pushed equipment {payload.EquipmentCode} data to PMIS.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PMIS push message from RabbitMQ");
                // Reject and requeue
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
    }

    private async Task PushToPmisAsync(PmisPushPayload payload, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("PMIS");
        
        await _httpRetryPolicy.ExecuteAsync(async () =>
        {
            var response = await client.PostAsJsonAsync(_pmisPushApiUrl, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
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
