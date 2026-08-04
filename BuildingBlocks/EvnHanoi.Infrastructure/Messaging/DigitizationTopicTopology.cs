using RabbitMQ.Client;

namespace EvnHanoi.Infrastructure.Messaging;

/// <summary>
/// Khai báo exchange/queue/bindings pipeline OCR — idempotent, an toàn khi Aspire khởi động song song.
/// </summary>
public static class DigitizationTopicTopology
{
    public const string ExchangeName = "digitization.topic";

    public const string OcrTaskQueue = "ocr_task_queue";
    public const string OcrTaskRoutingKey = "ocr.process.task";

    public const string ExtractionTaskQueue = "extraction_task_queue";
    public const string ExtractionTaskRoutingKey = "extraction.process.task";

    /// <summary>
    /// Hàng đợi lỗi (dead-letter) cho message đã thử lại quá số lần cho phép — hứng lại để kiểm tra
    /// thủ công, tránh mất dữ liệu (BACKEND_GUIDELINES §4.1). Đây là queue MỚI, không đổi argument
    /// của <see cref="OcrTaskQueue"/>/<see cref="ExtractionTaskQueue"/> hiện có (RabbitMQ không cho
    /// phép redeclare queue đang chạy với argument khác).
    /// </summary>
    public const string OcrTaskDeadLetterQueue = "ocr_task_queue_dlq";
    public const string OcrTaskDeadLetterRoutingKey = "ocr.process.task.failed";

    public const string ExtractionTaskDeadLetterQueue = "extraction_task_queue_dlq";
    public const string ExtractionTaskDeadLetterRoutingKey = "extraction.process.task.failed";

    public const string EquipmentProgressQueue = "equipment_digitization_progress_queue";
    public const string OcrProgressRoutingKey = "ocr.process.progress";
    public const string ExtractionProgressRoutingKey = "extraction.process.progress";

    public const string EquipmentCompletedQueue = "equipment_digitization_completed_queue";
    public const string ExtractionCompletedRoutingKey = "extraction.process.completed";

    public static async Task EnsureAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await DeclareAndBindAsync(channel, OcrTaskQueue, OcrTaskRoutingKey, cancellationToken);
        await DeclareAndBindAsync(channel, ExtractionTaskQueue, ExtractionTaskRoutingKey, cancellationToken);
        await DeclareAndBindAsync(channel, OcrTaskDeadLetterQueue, OcrTaskDeadLetterRoutingKey, cancellationToken);
        await DeclareAndBindAsync(channel, ExtractionTaskDeadLetterQueue, ExtractionTaskDeadLetterRoutingKey, cancellationToken);
        await DeclareAndBindAsync(channel, EquipmentProgressQueue, OcrProgressRoutingKey, cancellationToken);
        await DeclareAndBindAsync(channel, EquipmentProgressQueue, ExtractionProgressRoutingKey, cancellationToken);
        await DeclareAndBindAsync(channel, EquipmentCompletedQueue, ExtractionCompletedRoutingKey, cancellationToken);
        await DeclareAndBindAsync(
            channel,
            DocumentTextMessaging.IndexQueue,
            DocumentTextMessaging.ReindexRoutingKey,
            cancellationToken);
    }

    private static async Task DeclareAndBindAsync(
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
}
