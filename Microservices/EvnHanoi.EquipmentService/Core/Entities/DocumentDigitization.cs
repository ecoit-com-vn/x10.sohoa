namespace EvnHanoi.EquipmentService.Core.Entities;

/// <summary>
/// Tiến trình OCR / Extraction — map message worker (ocr.process.progress, extraction.process.progress).
/// FileId trên worker = DOCUMENT_VERSION_ID.
/// </summary>
public class DocumentOcrProgress
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string? Action { get; set; }
    /// <summary>ocr | extraction</summary>
    public string Phase { get; set; } = "ocr";
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int Progress { get; set; }
    /// <summary>Pending | Running | OcrCompleted | Extracting | Completed | Failed</summary>
    public string Status { get; set; } = "Pending";
    public string? ProcessOption { get; set; }
    public string? BucketName { get; set; }
    public string? FilePath { get; set; }
    /// <summary>Snapshot EAV FormSchema JSON gửi kèm request bóc tách.</summary>
    public string? FormJson { get; set; }
    public string? ErrorMessage { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Kết quả bóc tách LLM — map extraction.process.completed + JSON trên MinIO.
/// </summary>
public class DocumentExtractionResult
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public Guid? OcrProgressId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ResultJson { get; set; }
    public string? ResultFilePath { get; set; }
    public string? BucketName { get; set; }
    public string? FormJson { get; set; }
    public string? MergedDataJson { get; set; }
    public string? ErrorMessage { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
