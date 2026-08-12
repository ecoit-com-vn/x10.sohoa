namespace EvnHanoi.EquipmentService.Core.DTOs;

/// <summary>
/// Phạm vi trang cần bóc tách, gửi xuống DigitizationService qua message. Chỉ ảnh hưởng bước bóc
/// tách (gửi text lên LLM) — bước OCR luôn chạy đủ trang để PDF 2 lớp và tìm kiếm toàn văn không
/// bị hụt. Giá trị phải khớp EvnHanoi.DigitizationService.Models.ExtractionScopes (2 service không
/// tham chiếu lẫn nhau nên phải khai báo song song; đổi bên nào thì đổi cả bên kia).
/// </summary>
public static class ExtractionScopeValues
{
    public const string FirstPage = "FirstPage";
    public const string LastPage = "LastPage";
    public const string FirstAndLastPage = "FirstAndLastPage";
    public const string AllPages = "AllPages";

    /// <summary>Mặc định khi client không gửi: bóc tách mọi trang — giữ nguyên hành vi cũ.</summary>
    public const string Default = AllPages;

    /// <summary>Giá trị lạ/rỗng đều quy về <see cref="Default"/> để không đẩy rác xuống worker.</summary>
    public static string Normalize(string? scope) =>
        scope is FirstPage or LastPage or FirstAndLastPage or AllPages ? scope! : Default;
}

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
    public Guid? EquipmentId { get; set; }
    /// <summary>Phạm vi trang bóc tách — xem <see cref="ExtractionScopeValues"/>.</summary>
    public string ExtractionScope { get; set; } = ExtractionScopeValues.Default;
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
    public Guid? EquipmentId { get; set; }
    /// <summary>Phạm vi trang bóc tách — xem <see cref="ExtractionScopeValues"/>.</summary>
    public string ExtractionScope { get; set; } = ExtractionScopeValues.Default;
}

/// <summary>ocr.process.progress / extraction.process.progress / *.process.failed</summary>
public class DigitizationProgressMessage
{
    public Guid FileId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int Progress { get; set; }
    /// <summary>Chỉ có giá trị khi Action là "*.process.failed" — worker đã thử lại hết số lần cho phép.</summary>
    public string? ErrorMessage { get; set; }
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
    /// <summary>Thiết bị kỹ thuật — phân biệt kết quả bóc tách theo thiết bị vs hồ sơ.</summary>
    public Guid? EquipmentId { get; set; }
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
    /// <summary>
    /// Tùy chọn — FirstPage | LastPage | FirstAndLastPage | AllPages. Bỏ trống = AllPages (hành vi cũ).
    /// Chỉ giới hạn bước bóc tách, không ảnh hưởng bước OCR.
    /// </summary>
    public string? ExtractionScope { get; set; }
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
    /// <summary>
    /// Tùy chọn — FirstPage | LastPage | FirstAndLastPage | AllPages. Bỏ trống = AllPages (hành vi cũ).
    /// Chỉ giới hạn bước bóc tách, không ảnh hưởng bước OCR.
    /// </summary>
    public string? ExtractionScope { get; set; }
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
    /// <summary>Dùng cho phân hệ Module OCR (Nhóm A) — đọc lại kết quả OCR đã có, không xử lý lại.</summary>
    public string? BucketName { get; set; }
    public string? FilePath { get; set; }
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
    public Guid? EquipmentId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

/// <summary>Lưu dữ liệu form theo từng tài liệu (mergedDataJson) — không ghi đè formData hồ sơ.</summary>
public class SaveDocumentExtractionDataRequest
{
    public string MergedDataJson { get; set; } = string.Empty;

    /// <summary>
    /// Chỉ dùng khi lưu bóc tách theo thiết bị:
    /// false = chỉ lưu MergedDataJson;
    /// true = thay thế toàn bộ FormValues thiết bị bằng MergedDataJson.
    /// </summary>
    public bool UpdateEquipmentFormValues { get; set; }
}
