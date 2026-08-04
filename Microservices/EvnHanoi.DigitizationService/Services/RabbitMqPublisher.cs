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
        private readonly IConnection _connection;
        private readonly ILogger<RabbitMqPublisher> _logger;

        public RabbitMqPublisher(IConnection connection, ILogger<RabbitMqPublisher> logger)
        {
            _connection = connection;
            _logger = logger;
        }

        public async Task PublishMessageAsync<T>(T message, string exchange, string routingKey)
        {
            try
            {
                // Create a lightweight channel for publishing, reusing the shared TCP connection
                using var channel = await _connection.CreateChannelAsync();

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

        public async Task<bool> TryPublishMessageAsync<T>(T message, string exchange, string routingKey,
            int maxAttempts = 1, TimeSpan? initialDelay = null)
        {
            var delay = initialDelay ?? TimeSpan.Zero;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await PublishMessageAsync(message, exchange, routingKey);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "TryPublishMessageAsync thất bại (lần {Attempt}/{MaxAttempts}) tới exchange {Exchange}, routing key {RoutingKey}.",
                        attempt, maxAttempts, exchange, routingKey);

                    if (attempt >= maxAttempts) return false;

                    await Task.Delay(delay);
                    delay += delay;
                }
            }

            return false;
        }
    }
}
