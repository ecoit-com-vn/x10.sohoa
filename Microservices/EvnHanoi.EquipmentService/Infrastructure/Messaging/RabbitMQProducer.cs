using System.Text;
using System.Text.Json;
using EvnHanoi.EquipmentService.Core.Interfaces;
using RabbitMQ.Client;

namespace EvnHanoi.EquipmentService.Infrastructure.Messaging;

public class RabbitMQProducer : IMessageProducer
{
    private readonly IConfiguration _configuration;

    public RabbitMQProducer(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void SendMessage<T>(T message, string queueName)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest",
            Port = int.TryParse(_configuration["RabbitMQ:Port"], out var port) ? port : 5672
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
    }
}
