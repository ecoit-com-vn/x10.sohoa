namespace EvnHanoi.SyncService.Services;

/// <summary>URL + header đã giải mã, sẵn sàng để <c>PmisClient</c> gắn vào <see cref="HttpRequestMessage"/>.</summary>
public class ResolvedPmisEndpoint
{
    public required string ApiCode { get; init; }
    public required string DisplayName { get; init; }
    public required string Url { get; init; }
    public required string HttpMethod { get; init; }
    public int? TimeoutSeconds { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Tra cấu hình endpoint PMIS (URL + header) từ PMIS_API_ENDPOINT_CONFIG/PMIS_API_ENDPOINT_HEADER,
/// cache 5 phút (cùng quy ước TTL với DynamicPermissionFilter) để PmisClient không phải query DB
/// mỗi lần gọi PMIS. Gọi <see cref="Invalidate"/> ngay sau khi admin lưu cấu hình.
/// </summary>
public interface IPmisEndpointConfigProvider
{
    /// <summary>Trả null nếu API chưa cấu hình (IsActive=0 hoặc Url rỗng) — caller tự quyết định thông báo lỗi nghiệp vụ.</summary>
    Task<ResolvedPmisEndpoint?> GetEndpointAsync(string apiCode);
    void Invalidate(string apiCode);
}
