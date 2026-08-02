namespace EvnHanoi.DigitizationService.Models.OcrModule;

/// <summary>
/// Tạo Job từ 1 tài liệu đã có kết quả OCR sẵn trên MinIO — không gọi lại ocr_vl_server,
/// dùng được ngay cho tài liệu hồ sơ/thiết bị đã số hóa qua pipeline production hiện có.
/// </summary>
public class CreateJobFromExistingRequest
{
    public string Bucket { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? DocumentVersionId { get; set; }
    public int TotalPages { get; set; }
}

public class CreateJobResponse
{
    public string JobId { get; set; } = string.Empty;
    public int RegionCount { get; set; }
    public string State { get; set; } = string.Empty;
}

public class OcrModuleRegionDto
{
    public string Id { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public double BoxX0 { get; set; }
    public double BoxY0 { get; set; }
    public double BoxX1 { get; set; }
    public double BoxY1 { get; set; }
    public string TextRaw { get; set; } = string.Empty;
    public double? Confidence { get; set; }
    public string? ScriptType { get; set; }
    public string RegionType { get; set; } = "Text";
    public string? FormulaText { get; set; }
    public double? SealSignatureScore { get; set; }
    public string? SpellcheckSuggestion { get; set; }
    public string? SpellcheckStatus { get; set; }
    public string Status { get; set; } = "Detected";
}
