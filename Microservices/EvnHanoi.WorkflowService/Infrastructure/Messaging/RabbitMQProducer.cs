using System.Text;
using System.Text.Json;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace EvnHanoi.WorkflowService.Infrastructure.Messaging;

public class RabbitMQProducer : IMessageProducer
{
    private readonly IConnection _connection;

    public RabbitMQProducer(IConnection connection)
    {
        _connection = connection;
    }

    public async Task SendMessageAsync<T>(T message, string queueName)
    {
        using var channel = await _connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        await channel.BasicPublishAsync(exchange: "", routingKey: queueName, mandatory: false, basicProperties: new BasicProperties(), body: body);
    }

    public async Task PublishToExchangeAsync<T>(T message, string exchangeName, string routingKey)
    {
        using var channel = await _connection.CreateChannelAsync();

        if (string.Equals(exchangeName, NotificationTopicTopology.ExchangeName, StringComparison.OrdinalIgnoreCase))
            await NotificationTopicTopology.EnsureAsync(channel);

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: new BasicProperties(),
            body: body);
    }
}
