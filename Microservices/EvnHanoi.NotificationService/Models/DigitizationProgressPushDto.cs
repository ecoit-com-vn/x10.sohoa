namespace EvnHanoi.NotificationService.Models;

/// <summary>Push realtime tiến độ OCR/bóc tách tới clients đang xem hồ sơ.</summary>
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
    /// <summary>Chỉ gửi khi extraction completed/failed.</summary>
    public string? ExtractionStatus { get; set; }
}
