namespace EvnHanoi.EquipmentService.Core.DTOs;

/// <summary>
/// Màn hình giám sát job OCR/bóc tách toàn hệ thống — 1 dòng = 1 job (DOCUMENT_OCR_PROGRESS),
/// kèm kết quả bóc tách tương ứng nếu có. Chỉ đọc, không sửa gì đang chạy trong pipeline.
/// </summary>
public class OcrJobListItemDto
{
    public Guid ProgressId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string? DocumentTypeName { get; set; }

    public Guid? DossierId { get; set; }
    public string? DossierInfrastructureName { get; set; }
    public string? DossierInfrastructureCode { get; set; }

    public Guid? EquipmentId { get; set; }
    public string? EquipmentName { get; set; }

    /// <summary>ocr | extraction</summary>
    public string Phase { get; set; } = "ocr";
    /// <summary>Pending | Running | OcrCompleted | Extracting | Completed | Failed</summary>
    public string Status { get; set; } = "Pending";
    public int Progress { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public class OcrJobListFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Status { get; set; }
    public string? Phase { get; set; }
    public string? Keyword { get; set; }
    public Guid? DocumentTypeId { get; set; }
    /// <summary>Tìm theo tên/mã hồ sơ (trạm/đường dây) hoặc tên thiết bị liên quan tới job.</summary>
    public string? ResourceKeyword { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
