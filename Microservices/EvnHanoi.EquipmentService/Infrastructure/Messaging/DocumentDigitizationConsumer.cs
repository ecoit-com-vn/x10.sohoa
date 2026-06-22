using System.Text;
using System.Text.Json;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EvnHanoi.EquipmentService.Infrastructure.Messaging;

/// <summary>
/// Lắng nghe progress/completed từ DigitizationService (exchange digitization.topic).
/// </summary>
public class DocumentDigitizationConsumer : BackgroundService
{
    private const string ExchangeName = "digitization.topic";
    private const string ProgressQueue = "equipment_digitization_progress_queue";
    private const string CompletedQueue = "equipment_digitization_completed_queue";

    private readonly IConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentDigitizationConsumer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DocumentDigitizationConsumer(
        IConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentDigitizationConsumer> logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IChannel? channel = null;
        try
        {
            channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            await SetupQueueAsync(channel, ProgressQueue, "ocr.process.progress", stoppingToken);
            await SetupQueueAsync(channel, ProgressQueue, "extraction.process.progress", stoppingToken);
            await SetupQueueAsync(channel, CompletedQueue, "extraction.process.completed", stoppingToken);

            var progressConsumer = new AsyncEventingBasicConsumer(channel);
            progressConsumer.ReceivedAsync += async (_, ea) =>
            {
                await HandleMessageAsync(
                    channel,
                    ea,
                    async (text, service) =>
                    {
                        var msg = JsonSerializer.Deserialize<DigitizationProgressMessage>(text, JsonOptions);
                        if (msg != null)
                            await service.HandleProgressMessageAsync(msg);
                    },
                    stoppingToken);
            };

            var completedConsumer = new AsyncEventingBasicConsumer(channel);
            completedConsumer.ReceivedAsync += async (_, ea) =>
            {
                await HandleMessageAsync(
                    channel,
                    ea,
                    async (text, service) =>
                    {
                        var msg = JsonSerializer.Deserialize<DigitizationExtractionCompletedMessage>(text, JsonOptions);
                        if (msg != null)
                            await service.HandleExtractionCompletedAsync(msg);
                    },
                    stoppingToken);
            };

            await channel.BasicConsumeAsync(
                queue: ProgressQueue,
                autoAck: false,
                consumer: progressConsumer,
                cancellationToken: stoppingToken);

            await channel.BasicConsumeAsync(
                queue: CompletedQueue,
                autoAck: false,
                consumer: completedConsumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation(
                "DocumentDigitizationConsumer đang lắng nghe {ProgressQueue}, {CompletedQueue}",
                ProgressQueue, CompletedQueue);

            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(1000, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown bình thường
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khởi tạo DocumentDigitizationConsumer.");
        }
        finally
        {
            if (channel is not null)
                await channel.CloseAsync(cancellationToken: stoppingToken);
        }
    }

    private static async Task SetupQueueAsync(
        IChannel channel,
        string queueName,
        string routingKey,
        CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: ExchangeName,
            routingKey: routingKey,
            cancellationToken: cancellationToken);
    }

    private async Task HandleMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs ea,
        Func<string, IDocumentDigitizationService, Task> handler,
        CancellationToken cancellationToken)
    {
        var text = Encoding.UTF8.GetString(ea.Body.ToArray());
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDocumentDigitizationService>();
            await handler(text, service);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xử lý message digitization: {Message}", text);
        }
        finally
        {
            await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
        }
    }
}
