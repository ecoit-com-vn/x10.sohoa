using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Serilog;

namespace EvnHanoi.Infrastructure.Audit;

public sealed class RabbitMqAuditPublisher : IAuditPublisher
{
    private readonly IConnection _connection;

    public RabbitMqAuditPublisher(IConnection connection)
    {
        _connection = connection;
    }

    public void Publish(AuditEvent auditEvent)
    {
        _ = PublishAsync(auditEvent);
    }

    private async Task PublishAsync(AuditEvent auditEvent)
    {
        try
        {
            await using var channel = await _connection.CreateChannelAsync();
            await channel.QueueDeclareAsync(
                queue: AuditMessaging.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var json = JsonSerializer.Serialize(auditEvent, AuditJsonSerializer.Options);
            var body = Encoding.UTF8.GetBytes(json);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                DeliveryMode = DeliveryModes.Persistent
            };
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: AuditMessaging.QueueName,
                mandatory: false,
                basicProperties: properties,
                body: body);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Không thể publish audit event {AuditId} ({Action})", auditEvent.Id, auditEvent.Action);
        }
    }
}
