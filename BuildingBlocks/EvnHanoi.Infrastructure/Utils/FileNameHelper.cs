using System.Globalization;
using System.Text;

namespace EvnHanoi.Infrastructure.Utils;

/// <summary>
/// Chuẩn hóa tên file cho object key MinIO/S3. Tên hiển thị (DOCUMENTS.NAME) giữ nguyên tiếng Việt.
/// </summary>
public static class FileNameHelper
{
    /// <summary>Chuyển ký tự có dấu tiếng Việt sang không dấu (đ/Đ → d/D).</summary>
    public static string RemoveVietnameseDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var replaced = text
            .Replace("đ", "d")
            .Replace("Đ", "D");

        var normalized = replaced.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(replaced.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Tên file an toàn cho MinIO object key: bỏ dấu, loại ký tự path, chỉ giữ ASCII [a-zA-Z0-9._- ].
    /// </summary>
    public static string ToMinioObjectFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
            return "file.bin";

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            name = name.Replace(invalidChar, '_');

        name = name.Replace('/', '_').Replace('\\', '_');
        name = RemoveVietnameseDiacritics(name);

        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or ' ')
                sb.Append(c);
            else
                sb.Append('_');
        }

        var result = CollapseRepeated(sb.ToString().Trim(), '_');
        result = CollapseRepeated(result, ' ');

        return string.IsNullOrWhiteSpace(result) ? "file.bin" : result;
    }

    private static string CollapseRepeated(string value, char ch)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var doubled = new string(ch, 2);
        var result = value;
        while (result.Contains(doubled, StringComparison.Ordinal))
            result = result.Replace(doubled, ch.ToString(), StringComparison.Ordinal);

        return result;
    }
}
