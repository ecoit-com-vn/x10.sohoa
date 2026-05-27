using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EvnHanoi.DigitizationService.Services
{
    public class RabbitMqPublisher : IMessagePublisher
    {
        private readonly string _hostname;
        private readonly string _username;
        private readonly string _password;
        private readonly ILogger<RabbitMqPublisher> _logger;

        public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
        {
            _logger = logger;
            _hostname = configuration["RabbitMQ:HostName"] ?? "localhost";
            _username = configuration["RabbitMQ:UserName"] ?? "guest";
            _password = configuration["RabbitMQ:Password"] ?? "guest";
        }

        public async Task PublishMessageAsync<T>(T message, string exchange, string routingKey)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _hostname,
                    UserName = _username,
                    Password = _password
                };

                using var connection = await factory.CreateConnectionAsync();
                using var channel = await connection.CreateChannelAsync();

                // Ensure the exchange exists
                await channel.ExchangeDeclareAsync(exchange: exchange, type: ExchangeType.Topic, durable: true);

                var messageBody = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(messageBody);

                var properties = new BasicProperties
                {
                    Persistent = true
                };

                await channel.BasicPublishAsync(
                    exchange: exchange,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Published message to exchange {Exchange} with routing key {RoutingKey}", exchange, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to exchange {Exchange}", exchange);
                throw;
            }
        }
    }
}
