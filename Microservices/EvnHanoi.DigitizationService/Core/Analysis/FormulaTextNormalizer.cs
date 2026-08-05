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

    // "số/số" cũng khớp định dạng ngày (DD/MM/YYYY) và số trang (Trang: X/Y) — 2 dạng cực kỳ phổ biến
    // trong biên bản kỹ thuật, KHÔNG phải công thức. Loại rõ các trường hợp này trước khi kết luận.
    private static readonly Regex DatePattern = new(
        @"\b\d{1,2}\s*/\s*\d{1,2}\s*/\s*\d{2,4}\b", RegexOptions.Compiled); // DD/MM/YYYY hoặc DD/MM/YY

    private static readonly Regex BareTwoPartFraction = new(
        @"^\s*(\d{1,2})\s*/\s*(\d{1,2})\s*$", RegexOptions.Compiled);

    private static readonly string[] DateOrPageHintKeywords = ["ngày", "trang", "date", "page"];

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

        if (MathSymbolPattern.IsMatch(text) || ExponentCaretPattern.IsMatch(text)) return true;

        if (FractionPattern.IsMatch(text) && !LooksLikeDateOrPageReference(text)) return true;

        return false;
    }

    /// <summary>
    /// "Ngày : 31/05/2025", "Trang : 02/02", hay "01/02" đứng riêng đều khớp mẫu số/số nhưng là ngày
    /// tháng/số trang, không phải tỉ số kỹ thuật — tỉ số/công thức thật thường có số lớn hơn hoặc có phần
    /// thập phân (vd. "22/0.4kV", "4.2/230.94"), không rơi hết vào khoảng ngày (1-31) VÀ tháng (1-12).
    /// </summary>
    private static bool LooksLikeDateOrPageReference(string text)
    {
        if (DatePattern.IsMatch(text)) return true;

        foreach (var keyword in DateOrPageHintKeywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        }

        var bareMatch = BareTwoPartFraction.Match(text);
        if (bareMatch.Success)
        {
            var first = int.Parse(bareMatch.Groups[1].Value);
            var second = int.Parse(bareMatch.Groups[2].Value);
            if (first is >= 1 and <= 31 && second is >= 1 and <= 12) return true;
        }

        return false;
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
