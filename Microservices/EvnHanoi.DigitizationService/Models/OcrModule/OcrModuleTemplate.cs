namespace EvnHanoi.DigitizationService.Models.OcrModule;

public class OcrModuleTemplateSnapshot
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DocumentTypeCode { get; set; }
    public string? SourceJobId { get; set; }
    public string ReferenceRegionsJson { get; set; } = "[]";
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}

/// <summary>1 vùng tham chiếu trong mẫu chuẩn — snapshot từ OCR_MODULE_REGION tại thời điểm lưu mẫu.</summary>
public class TemplateRegionSnapshot
{
    public int PageNumber { get; set; }
    public double BoxX0 { get; set; }
    public double BoxY0 { get; set; }
    public double BoxX1 { get; set; }
    public double BoxY1 { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class OcrModuleTemplateDiffResult
{
    public string Id { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string TemplateSnapshotId { get; set; } = string.Empty;
    public string? RegionId { get; set; }
    /// <summary>Missing | Extra | TextMismatch | PositionShift</summary>
    public string DiffType { get; set; } = string.Empty;
    public string? Detail { get; set; }
    /// <summary>Flagged | Confirmed | Rejected</summary>
    public string Status { get; set; } = "Flagged";
    public int PageNumber { get; set; }
}
