namespace EvnHanoi.DigitizationService.Models.OcrModule;

/// <summary>
/// Job trung tâm của phân hệ Module OCR — 1 file scan (mới upload hoặc tài liệu đã số hóa từ trước)
/// đi qua các bước phân tích Nhóm A (88, 90, 92, 93, 94, 95).
/// </summary>
public class OcrModuleJob
{
    public string Id { get; set; } = string.Empty;
    public string SourceType { get; set; } = "NewUpload"; // "NewUpload" | "ExistingDocument"
    public string SourceBucket { get; set; } = string.Empty;
    public string SourceFilePath { get; set; } = string.Empty;
    public string? SourceDocumentVersionId { get; set; }
    public int TotalPages { get; set; }
    public string State { get; set; } = "Materializing"; // Materializing | Ready | Failed
    public string? ErrorMessage { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
    public int RowVersion { get; set; } = 1;
}
