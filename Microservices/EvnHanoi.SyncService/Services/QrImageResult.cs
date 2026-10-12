namespace EvnHanoi.SyncService.Services;

public enum QrImageOutcome
{
    /// <summary>Tải được ảnh QR.</summary>
    Ok,

    /// <summary>PMIS trả 404 — thiết bị không có ảnh QR (kết quả cuối, KHÔNG thử lại).</summary>
    NotFound,

    /// <summary>Lỗi tạm thời (5xx, timeout, mạng) hoặc ảnh rỗng — thử lại ở lượt sau.</summary>
    Error,

    /// <summary>API DEVICE_QR_IMAGE chưa cấu hình/đang tắt.</summary>
    NotConfigured
}

/// <summary>Kết quả tải ảnh QR của 1 thiết bị. Phân biệt "không có ảnh" (404 — không bao giờ thử lại) với lỗi tạm thời, để thiết bị không có QR
/// không bị coi là "chưa đủ chi tiết" và bị gọi lại mãi (đo thực tế 08–09/10/2026: 5.308/16.910 lần gọi QR là 404 trên 5.257 thiết bị).</summary>
public sealed record QrImageResult(QrImageOutcome Outcome, byte[]? Bytes = null, string? Reason = null)
{
    /// <summary>true nếu kết quả là cuối cùng và thiết bị được coi là ĐỦ chi tiết (có ảnh, hoặc PMIS xác nhận không có ảnh).</summary>
    public bool IsFinal => Outcome is QrImageOutcome.Ok or QrImageOutcome.NotFound;

    /// <summary>Phân loại 1 ngoại lệ HTTP khi tải QR: 404 → NotFound, còn lại → Error.</summary>
    public static QrImageResult FromException(Exception ex) =>
        ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound }
            ? new QrImageResult(QrImageOutcome.NotFound, null, "PMIS trả 404: thiết bị không có ảnh QR")
            : new QrImageResult(QrImageOutcome.Error, null, ex.Message);
}
