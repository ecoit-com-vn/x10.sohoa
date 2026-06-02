using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Repositories;
using EvnHanoi.DigitizationService.Services;

namespace EvnHanoi.DigitizationService.Workers
{
    public class OcrTaskConsumer : BackgroundService
    {
        private readonly ILogger<OcrTaskConsumer> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConnection _connection;
        private IChannel? _channel;

        public OcrTaskConsumer(
            ILogger<OcrTaskConsumer> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            IConnection connection)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _connection = connection;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                var exchangeName = "digitization.topic";
                var queueName = "ocr_task_queue";
                var routingKey = "ocr.process.task";

                // Declare Exchange and Queue
                await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic, durable: true, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var messageText = Encoding.UTF8.GetString(body);
                    _logger.LogInformation("Received OCR task message: {Message}", messageText);

                    try
                    {
                        var taskMsg = JsonSerializer.Deserialize<OcrTaskMessage>(messageText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (taskMsg != null)
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var repository = scope.ServiceProvider.GetRequiredService<IFileAttachmentRepository>();

                            // 1. Update status to Processing
                            await repository.UpdateStatusAsync(taskMsg.FileId, "Processing");
                            _logger.LogInformation("Updated FileAttachment {FileId} status to Processing.", taskMsg.FileId);

                            // 2. Simulated OCR Workload
                            _logger.LogInformation("Downloading scan document from MinIO bucket {BucketName}: {FilePath}", taskMsg.BucketName, taskMsg.FilePath);
                            await Task.Delay(3000, stoppingToken); // Simulate OCR OCR processing latencies
                            _logger.LogInformation("AI OCR parsed document text successfully for FileId {FileId}.", taskMsg.FileId);

                            // 3. Update status to Completed
                            await repository.UpdateStatusAsync(taskMsg.FileId, "Completed");
                            _logger.LogInformation("AI OCR completed. Updated FileAttachment {FileId} status to Completed.", taskMsg.FileId);

                            // 4. Acknowledge message
                            await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        }
                        else
                        {
                            _logger.LogWarning("Null task message parsed. Dropping message.");
                            await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing OCR task.");
                        await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                    }
                };

                await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect or consume OCR tasks from RabbitMQ.");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel is not null) await _channel.CloseAsync(cancellationToken: cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}
