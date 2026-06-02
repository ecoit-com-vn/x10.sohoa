using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.Interfaces;
using RabbitMQ.Client;

namespace EvnHanoi.EquipmentService.Infrastructure.Messaging;

public class RabbitMQProducer : IMessageProducer
{
    private readonly IConnection _connection;

    public RabbitMQProducer(IConnection connection)
    {
        _connection = connection;
    }

    public async Task SendMessageAsync<T>(T message, string queueName)
    {
        // Create a lightweight virtual channel, reusing the shared TCP connection
        using var channel = await _connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        await channel.BasicPublishAsync(exchange: "", routingKey: queueName, mandatory: false, basicProperties: new BasicProperties(), body: body);
    }
}
