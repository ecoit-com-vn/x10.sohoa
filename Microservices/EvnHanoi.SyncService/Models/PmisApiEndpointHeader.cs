namespace EvnHanoi.SyncService.Models;

/// <summary>
/// 1 header (key/value) gửi kèm khi gọi 1 API PMIS — value được mã hoá tại chỗ nếu IsSecret = true.
/// </summary>
public class PmisApiEndpointHeader
{
    public string Id { get; set; } = string.Empty;
    public string EndpointConfigId { get; set; } = string.Empty;
    public string HeaderKey { get; set; } = string.Empty;

    /// <summary>Giá trị lưu trong DB — đã mã hoá (AES-GCM) nếu IsSecret = true.</summary>
    public string? HeaderValue { get; set; }
    public bool IsSecret { get; set; }
}

public class PmisApiEndpointHeaderDto
{
    public string Id { get; set; } = string.Empty;
    public string HeaderKey { get; set; } = string.Empty;

    /// <summary>Ẩn dạng "••••••" nếu IsSecret = true — giá trị thật không bao giờ trả về qua API sau khi lưu.</summary>
    public string? HeaderValue { get; set; }
    public bool IsSecret { get; set; }
}

/// <summary>
/// 1 dòng header gửi lên khi lưu (thay toàn bộ danh sách header của 1 endpoint).
/// Với header IsSecret=true đã tồn tại: để trống HeaderValue để GIỮ NGUYÊN giá trị đã lưu
/// (tránh phải round-trip giá trị bí mật đã mã hoá ra ngoài client).
/// </summary>
public class UpsertPmisApiEndpointHeaderRequest
{
    public string? Id { get; set; }
    public string HeaderKey { get; set; } = string.Empty;
    public string? HeaderValue { get; set; }
    public bool IsSecret { get; set; }
}

public class ReplacePmisApiEndpointHeadersRequest
{
    public List<UpsertPmisApiEndpointHeaderRequest> Headers { get; set; } = [];
}
