namespace EvnHanoi.DigitizationService.Models.OcrModule;

/// <summary>
/// 1 vùng văn bản/trang, materialize từ file JSON OCR đã có sẵn trên MinIO (do OcrWorker ghi ra
/// cho mọi tài liệu, kể cả tài liệu hồ sơ/thiết bị đang chạy production). Dùng chung cho 88/90/92/93/94/95.
/// </summary>
public class OcrModuleRegion
{
    public string Id { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public double BoxX0 { get; set; }
    public double BoxY0 { get; set; }
    public double BoxX1 { get; set; }
    public double BoxY1 { get; set; }
    public string TextRaw { get; set; } = string.Empty;
    public double? Confidence { get; set; }

    /// <summary>Printed | Handwritten | Mixed — yêu cầu 93.</summary>
    public string? ScriptType { get; set; }

    /// <summary>Text | Seal | Signature | Formula — yêu cầu 94/88.</summary>
    public string RegionType { get; set; } = "Text";

    /// <summary>Chỉ có giá trị khi RegionType = Formula — yêu cầu 88.</summary>
    public string? FormulaText { get; set; }

    /// <summary>Điểm khớp heuristic (không phải confidence AI thật) — yêu cầu 94.</summary>
    public double? SealSignatureScore { get; set; }

    /// <summary>Gợi ý sửa chính tả — yêu cầu 95.</summary>
    public string? SpellcheckSuggestion { get; set; }

    /// <summary>Pending | Accepted | Rejected | ManuallyEdited — yêu cầu 95.</summary>
    public string? SpellcheckStatus { get; set; }

    /// <summary>Detected | Recognized | Edited | Confirmed.</summary>
    public string Status { get; set; } = "Detected";

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
    public int RowVersion { get; set; } = 1;
}
