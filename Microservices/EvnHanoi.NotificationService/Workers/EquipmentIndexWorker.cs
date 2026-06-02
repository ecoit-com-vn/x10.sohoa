using Elastic.Clients.Elasticsearch;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EvnHanoi.NotificationService.Workers;

public class EquipmentIndexWorker : BackgroundService
{
    private readonly ILogger<EquipmentIndexWorker> _logger;
    private readonly ElasticsearchClient _elasticClient;
    private readonly IConfiguration _configuration;
    private readonly NotificationDispatcher _notificationDispatcher;
    private readonly IConnection _connection;
    private IChannel? _channel;

    public EquipmentIndexWorker(
        ILogger<EquipmentIndexWorker> logger, 
        ElasticsearchClient elasticClient, 
        IConfiguration configuration,
        NotificationDispatcher notificationDispatcher,
        IConnection connection)
    {
        _logger = logger;
        _elasticClient = elasticClient;
        _configuration = configuration;
        _notificationDispatcher = notificationDispatcher;
        _connection = connection;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
            
            var queueName = "equipment_sync_queue";
            await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation("Received message from RabbitMQ: {Message}", message);

                try
                {
                    // Idempotent processing: Update or Insert into Elasticsearch using document Id
                    var equipment = JsonSerializer.Deserialize<Equipment>(message, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (equipment != null && !string.IsNullOrEmpty(equipment.Id))
                    {
                        var response = await _elasticClient.IndexAsync(equipment, idx => idx.Index("equipments").Id(equipment.Id));
                        if (response.IsValidResponse)
                        {
                            _logger.LogInformation("Successfully indexed equipment {EquipmentId} to Elasticsearch.", equipment.Id);
                            await _notificationDispatcher.SendNotificationAsync($"Equipment {equipment.Name} synced successfully.");
                            
                            // Acknowledge message
                            await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        }
                        else
                        {
                            _logger.LogError("Failed to index equipment {EquipmentId}: {Error}", equipment.Id, response.DebugInformation);
                            // Nack and requeue
                            await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Invalid equipment data or missing Id. Dropping message.");
                        await _channel.BasicAckAsync(ea.DeliveryTag, false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

            // Keep running until cancellation requested
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect or consume from RabbitMQ.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null) await _channel.CloseAsync(cancellationToken: cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
