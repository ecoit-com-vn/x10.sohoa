namespace EvnHanoi.DocumentProcessing;

/// <summary>
/// Phân biệt PDF "scan" (ảnh thuần, không có lớp text trích xuất được) với PDF điện tử gốc
/// (có lớp text/vector thật) — chỉ PDF scan mới nên bị rasterize/nén DPI.
/// </summary>
internal static class PdfTextLayerDetector
{
    private const int MaxPagesToSample = 5;
    private const int MinNonWhitespaceCharsForText = 20;

    internal enum Classification
    {
        /// <summary>Không trang nào (trong số trang đã lấy mẫu) có lớp text đáng kể — coi là ảnh scan.</summary>
        Scanned,

        /// <summary>Có ít nhất 1 trang có lớp text thật (hoặc PDF hỗn hợp) — giữ nguyên, không rasterize.</summary>
        BornDigital,

        /// <summary>Không mở/đọc được PDF bằng PdfPig — coi như lỗi phân loại, không nén.</summary>
        Unknown
    }

    internal static Classification Classify(byte[] pdfBytes)
    {
        try
        {
            using var document = UglyToad.PdfPig.PdfDocument.Open(pdfBytes);
            if (document.NumberOfPages == 0)
                return Classification.Unknown;

            var pagesToCheck = Math.Min(document.NumberOfPages, MaxPagesToSample);

            // PDF hỗn hợp (vài trang có text thật, vài trang scan) được coi là BornDigital để tránh
            // phá lớp text hợp lệ của các trang điện tử — ưu tiên an toàn dữ liệu hơn tối ưu dung lượng.
            for (var pageNumber = 1; pageNumber <= pagesToCheck; pageNumber++)
            {
                var text = document.GetPage(pageNumber).Text;
                var nonWhitespaceCount = 0;
                if (!string.IsNullOrEmpty(text))
                {
                    foreach (var c in text)
                    {
                        if (!char.IsWhiteSpace(c) && ++nonWhitespaceCount >= MinNonWhitespaceCharsForText)
                            return Classification.BornDigital;
                    }
                }
            }

            return Classification.Scanned;
        }
        catch
        {
            return Classification.Unknown;
        }
    }
}
