using RabbitMQ.Client;

namespace EvnHanoi.Infrastructure.Messaging;

/// <summary>
/// Khai báo exchange/queue/bindings cho sự kiện phát sinh thông báo (hồ sơ chuyển bước, thiết bị
/// chuyển TBA/chuyển hồ sơ) — idempotent, an toàn khi nhiều service khởi động song song.
/// </summary>
public static class NotificationTopicTopology
{
    public const string ExchangeName = "notification.events.exchange";
    public const string QueueName = "notification.queue";

    public const string DossierMovedRoutingKey = "dossier.moved";
    public const string EquipmentTbaTransferredRoutingKey = "equipment.tba-transferred";
    public const string EquipmentDossierTransferredRoutingKey = "equipment.dossier-transferred";

    public static async Task EnsureAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(QueueName, ExchangeName, DossierMovedRoutingKey, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(QueueName, ExchangeName, EquipmentTbaTransferredRoutingKey, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(QueueName, ExchangeName, EquipmentDossierTransferredRoutingKey, cancellationToken: cancellationToken);
    }
}
