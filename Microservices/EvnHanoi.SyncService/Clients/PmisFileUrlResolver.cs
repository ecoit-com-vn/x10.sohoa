namespace EvnHanoi.SyncService.Clients;

/// <summary>
/// Chuẩn hoá URL tải file tài liệu PMIS (trường "file" của API 8/9) về đúng gateway đã cấu hình. PMIS trả về
/// URL không ổn định giữa các môi trường/lần gọi: IP nội bộ (http://10.9.185.67:30005/api/...), gateway thật
/// (https://gwlocal.evnhanoi.vn/...), gateway demo (https://demogwlan.evnhanoi.vn/...) hoặc chỉ đường dẫn
/// tương đối (/api/PmisDongBo/TaiFileTaiLieu?...). IP nội bộ không truy cập được từ cluster, nên MỌI dạng đều
/// được đưa về scheme + host + cổng của gateway cấu hình ("Endpoints:PMIS", cũng là BaseAddress của
/// HttpClient "PMIS"); đường dẫn và query giữ nguyên.
/// </summary>
internal static class PmisFileUrlResolver
{
    /// <param name="fileUrl">Giá trị thô của trường "file" (tuyệt đối hoặc tương đối).</param>
    /// <param name="gatewayBase">Gateway cấu hình (BaseAddress của HttpClient "PMIS"), có thể là giá trị mặc định mock.</param>
    /// <param name="endpointUrl">URL đã cấu hình của endpoint nguồn (SUBSTATION/LINE_DOCUMENT_LIST) — dự phòng khi
    /// gateway cấu hình thiếu hoặc còn là giá trị mặc định mock.</param>
    public static string Resolve(string fileUrl, Uri? gatewayBase, string? endpointUrl)
    {
        var raw = fileUrl.Trim();
        // "//host/path" (scheme-relative): trên Linux Uri.TryCreate(Absolute) hiểu nhầm thành đường dẫn file://.
        if (raw.StartsWith("//", StringComparison.Ordinal)) raw = "https:" + raw;
        var authority = UsableAuthority(gatewayBase) ?? UsableAuthority(endpointUrl);

        if (Uri.TryCreate(raw, UriKind.Absolute, out var abs) && (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
        {
            return authority == null ? raw : authority + abs.PathAndQuery;
        }

        // Đường dẫn tương đối ("/api/..." hoặc "api/...").
        if (authority == null) return raw;
        return authority + (raw.StartsWith('/') ? raw : "/" + raw);
    }

    private static string? UsableAuthority(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? UsableAuthority(uri) : null;

    private static string? UsableAuthority(Uri? uri)
    {
        if (uri == null || !uri.IsAbsoluteUri) return null;
        // "https://api.pmis.mock/" là giá trị mặc định khi thiếu Endpoints:PMIS — không dùng để tải file thật.
        if (uri.Host.EndsWith(".mock", StringComparison.OrdinalIgnoreCase)) return null;
        return uri.GetLeftPart(UriPartial.Authority);
    }
}
