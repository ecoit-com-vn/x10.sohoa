namespace EvnHanoi.EquipmentService.Core.DTOs;

/// <summary>
/// Payload gửi tới DigitizationService — khớp OcrTaskMessage worker.
/// FileId = DocumentVersionId.
/// </summary>
public class OcrTaskPublishMessage
{
    public Guid FileId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string Action { get; set; } = "ocr.process.task";
    public string ProcessOption { get; set; } = "OcrAndExtract";
    public string? ExtractPrompt { get; set; }
    public DigitizationExtractionForm? Form { get; set; }
    /// <summary>Snapshot đầy đủ EAV FormSchema JSON để worker/LLM tham chiếu.</summary>
    public string? FormSchemaJson { get; set; }
}

public class DigitizationExtractionForm
{
    public string FormId { get; set; } = string.Empty;
    public string FormName { get; set; } = string.Empty;
    public List<DigitizationExtractionFormField> Fields { get; set; } = new();
}

public class DigitizationExtractionFormField
{
    public string FieldName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ExtractionTaskPublishMessage
{
    public Guid FileId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string? ExtractPrompt { get; set; }
    public DigitizationExtractionForm? Form { get; set; }
    public string? FormSchemaJson { get; set; }
}

/// <summary>ocr.process.progress / extraction.process.progress</summary>
public class DigitizationProgressMessage
{
    public Guid FileId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int Progress { get; set; }
}

/// <summary>Payload push SignalR — khớp NotificationService.DigitizationProgressPushDto.</summary>
public class DigitizationProgressPushDto
{
    public Guid DossierId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string Phase { get; set; } = "ocr";
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public string? ExtractionStatus { get; set; }
}

/// <summary>extraction.process.completed</summary>
public class DigitizationExtractionCompletedMessage
{
    public Guid FileId { get; set; }
    public string Action { get; set; } = "extraction.process.completed";
    public string ResultFile { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string Status { get; set; } = "Success";
}

/// <summary>Body API POST .../documents/{versionId}/digitization</summary>
public class SubmitDossierDocumentDigitizationRequest
{
    /// <summary>OcrAndExtract | ExtractOnly</summary>
    public string ProcessOption { get; set; } = "OcrAndExtract";
    /// <summary>Tùy chọn — nếu bỏ trống lấy từ EavFormTemplate.ExtractionProcess của form loại văn bản.</summary>
    public string? ExtractPrompt { get; set; }
    /// <summary>Tùy chọn — nếu bỏ trống sẽ lấy từ form EAV của loại hồ sơ.</summary>
    public string? FormSchemaJson { get; set; }
}

public class SubmitDocumentDigitizationRequest
{
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    /// <summary>OcrAndExtract | ExtractOnly</summary>
    public string ProcessOption { get; set; } = "OcrAndExtract";
    public string FormId { get; set; } = string.Empty;
    public string FormName { get; set; } = string.Empty;
    /// <summary>EAV FormSchema JSON — bắt buộc để bóc tách đúng trường.</summary>
    public string FormSchemaJson { get; set; } = string.Empty;
    public string? ExtractPrompt { get; set; }
}

public class DocumentOcrProgressDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string? Action { get; set; }
    public string Phase { get; set; } = string.Empty;
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int Progress { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ProcessOption { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public class DocumentExtractionResultDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResultJson { get; set; }
    public string? ResultFilePath { get; set; }
    public string? MergedDataJson { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

/// <summary>Lưu dữ liệu form theo từng tài liệu (mergedDataJson) — không ghi đè formData hồ sơ.</summary>
public class SaveDocumentExtractionDataRequest
{
    public string MergedDataJson { get; set; } = string.Empty;
}
