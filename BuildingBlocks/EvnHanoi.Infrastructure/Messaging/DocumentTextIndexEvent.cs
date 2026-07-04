namespace EvnHanoi.Infrastructure.Messaging;

/// <summary>
/// Yêu cầu index/xóa nội dung OCR tài liệu trên Elasticsearch.
/// FileId trong DigitizationService = DocumentVersionId.
/// </summary>
public record DocumentTextIndexEvent(
    string DocumentVersionId,
    string BucketName,
    string FilePath,
    int TotalPages,
    string Action,
    DateTime OccurredAt
);

public static class DocumentTextIndexActions
{
    public const string Index = "Index";
    public const string Delete = "Delete";
}

public static class DocumentTextMessaging
{
    public const string IndexQueue = "document_text_index_queue";
    public const string IndexName = "document_index";
    public const string OcrCompletedRoutingKey = "ocr.process.completed";
    public const string ReindexRoutingKey = "document.text.reindex";
}
