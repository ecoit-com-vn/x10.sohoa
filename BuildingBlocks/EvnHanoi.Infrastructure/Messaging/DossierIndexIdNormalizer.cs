namespace EvnHanoi.Infrastructure.Messaging;

/// <summary>
/// Chuẩn hóa id (dossier/user GUID) cho Elasticsearch — _id document luôn dạng canonical lowercase D.
/// </summary>
public static class DossierIndexIdNormalizer
{
    public static string Normalize(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;

        return Guid.TryParse(id.Trim(), out var guid)
            ? guid.ToString("D").ToLowerInvariant()
            : id.Trim().ToLowerInvariant();
    }

    public static string? NormalizeOrNull(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return Normalize(id);
    }

    /// <summary>
    /// Các biến thể GUID dùng Term/Terms query — khớp ES doc index trước/sau chuẩn hóa (D/N, hoa/thường).
    /// </summary>
    public static IReadOnlyList<string> GetGuidTermVariants(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Array.Empty<string>();

        var trimmed = id.Trim();
        if (!Guid.TryParse(trimmed, out var guid))
        {
            return new HashSet<string>(StringComparer.Ordinal)
            {
                trimmed,
                trimmed.ToLowerInvariant(),
                trimmed.ToUpperInvariant()
            }.ToList();
        }

        return new HashSet<string>(StringComparer.Ordinal)
        {
            trimmed,
            guid.ToString("D"),
            guid.ToString("D").ToLowerInvariant(),
            guid.ToString("D").ToUpperInvariant(),
            guid.ToString("N"),
            guid.ToString("N").ToLowerInvariant(),
            guid.ToString("N").ToUpperInvariant()
        }.ToList();
    }
}
