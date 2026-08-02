using System.Text;
using System.Text.RegularExpressions;

namespace EvnHanoi.DigitizationService.Core.Analysis;

/// <summary>
/// Yêu cầu 88 — nhận diện + chuẩn hóa vùng văn bản "giống công thức kỹ thuật" bằng regex/heuristic
/// thuần C# trên text OCR đã có sẵn (KHÔNG dùng model OCR công thức chuyên dụng, không xử lý ảnh).
/// Giới hạn: đây là nhận diện + chuẩn hóa định dạng text, không dựng lại công thức toán học phức tạp
/// dạng ảnh/LaTeX — cần nêu rõ khi nghiệm thu.
/// </summary>
public static class FormulaTextNormalizer
{
    private static readonly Regex MathSymbolPattern = new(
        @"[√∑∆±≤≥≠∞ΩπΔΣ]|[²³⁰¹⁴⁵⁶⁷⁸⁹]", RegexOptions.Compiled);

    private static readonly Regex FractionPattern = new(
        @"\d+\s*/\s*\d+", RegexOptions.Compiled);

    private static readonly Regex ExponentCaretPattern = new(
        @"[A-Za-zÀ-ỹ0-9]\^\d+", RegexOptions.Compiled);

    private static readonly Regex SuperscriptDigitPattern = new(
        @"([A-Za-zÀ-ỹ)\]])([²³⁰¹⁴⁵⁶⁷⁸⁹]+)", RegexOptions.Compiled);

    private static readonly Dictionary<char, char> SuperscriptMap = new()
    {
        ['⁰'] = '0', ['¹'] = '1', ['²'] = '2', ['³'] = '3', ['⁴'] = '4',
        ['⁵'] = '5', ['⁶'] = '6', ['⁷'] = '7', ['⁸'] = '8', ['⁹'] = '9',
    };

    /// <summary>Trả về true nếu văn bản có dấu hiệu là công thức kỹ thuật/toán học.</summary>
    public static bool LooksLikeFormula(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        return MathSymbolPattern.IsMatch(text)
            || FractionPattern.IsMatch(text)
            || ExponentCaretPattern.IsMatch(text);
    }

    /// <summary>Chuẩn hóa định dạng: đổi số mũ unicode (x²) thành ký hiệu caret (x^2), gọn khoảng trắng.</summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var normalized = SuperscriptDigitPattern.Replace(text, m =>
        {
            var basePart = m.Groups[1].Value;
            var digits = new StringBuilder();
            foreach (var c in m.Groups[2].Value)
            {
                digits.Append(SuperscriptMap.TryGetValue(c, out var d) ? d : c);
            }
            return $"{basePart}^{digits}";
        });

        normalized = Regex.Replace(normalized, @"\s*([+\-=/])\s*", " $1 ");
        normalized = Regex.Replace(normalized, @"\s{2,}", " ").Trim();

        return normalized;
    }
}
