using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EvnHanoi.SyncService.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;

namespace EvnHanoi.SyncService.Workers;

public class PmisSyncWorker : BackgroundService
{
    private readonly ILogger<PmisSyncWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AsyncRetryPolicy _httpRetryPolicy;
    private readonly RetryPolicy _rabbitRetryPolicy;
    
    private IConnection? _connection;
    private IModel? _channel;
    private readonly string _queueName = "equipment_sync_queue";
    private readonly string _pmisApiUrl = "http://pmis.evn.com.vn/api/equipment/getAll";

    public PmisSyncWorker(ILogger<PmisSyncWorker> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;

        // Tích hợp Polly Retry Policy cho HTTP Client
        _httpRetryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning($"PMIS Sync HTTP call failed. Retry {retryCount} after {timeSpan.TotalSeconds}s. Error: {exception.Message}");
                });

        // Polly Retry Policy cho RabbitMQ
        _rabbitRetryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning($"RabbitMQ Connection failed in PMIS Sync. Retry {retryCount} after {timeSpan.TotalSeconds}s. Error: {exception.Message}");
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
            _logger.LogInformation($"PmisSyncWorker initialized RabbitMQ connection. Target queue: {_queueName}");
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromMinutes(10); // Định kỳ gọi sang PMIS

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("PmisSyncWorker is starting sync from PMIS...");

            try
            {
                await SyncFromPmisAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during PMIS sync process in PmisSyncWorker.");
            }

            _logger.LogInformation($"PmisSyncWorker sleeping for {delay.TotalMinutes} minutes before next sync.");
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task SyncFromPmisAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("PMIS");
        
        var responseString = await _httpRetryPolicy.ExecuteAsync(async () =>
        {
            // Sử dụng một API giả định ví dụ: http://pmis.evn.com.vn/api/equipment/getAll
            var response = await client.GetAsync(_pmisApiUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        });

        try
        {
            var equipments = JsonSerializer.Deserialize<EquipmentSyncMessage[]>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (equipments != null && _channel != null)
            {
                foreach (var equipment in equipments)
                {
                    var messageString = JsonSerializer.Serialize(equipment);
                    var body = Encoding.UTF8.GetBytes(messageString);

                    // Lấy dữ liệu về, sau đó push vào RabbitMQ
                    _channel.BasicPublish(
                        exchange: "",
                        routingKey: _queueName,
                        basicProperties: null,
                        body: body);
                }
                _logger.LogInformation($"PmisSyncWorker successfully pushed {equipments.Length} equipment records from PMIS to RabbitMQ queue {_queueName}.");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse PMIS response as EquipmentSyncMessage[]. Response might be empty or in different format.");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _connection?.Close();
        return base.StopAsync(cancellationToken);
    }
}
