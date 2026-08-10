using EvnHanoi.DigitizationService.Workers;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using SixLabors.ImageSharp;

namespace EvnHanoi.DigitizationService.Services;

/// <summary>
/// Dựng "PDF 2 lớp" (ảnh trang scan + lớp text ẩn nhưng searchable) từng trang. Đăng ký Scoped —
/// một instance dùng cho đúng 1 message OCR/1 tài liệu; tài nguyên ảnh của TẤT CẢ các trang phải
/// được giữ sống tới khi PdfDocument.Save() của caller chạy xong rồi mới Dispose (xem AddPage).
/// </summary>
public interface ISearchablePdfBuilder : IDisposable
{
    (int WidthPx, int HeightPx) GetImagePixelSize(byte[] jpegBytes);

    /// <summary>Thêm 1 trang, trả về số box thực sự vẽ được text (dùng để phát hiện lớp text rỗng toàn tài liệu).</summary>
    int AddPage(PdfDocument document, byte[] jpegBytes, IReadOnlyList<TextBoxResponse> ocrResults, double dpi = 200);

    void MarkAsSearchable(PdfDocument document);

    bool IsAlreadySearchable(byte[] pdfBytes);
}

public class SearchablePdfBuilder : ISearchablePdfBuilder
{
    private const string SearchableMarkerKey = "/EvnOcrVersion";
    private const string SearchableMarkerValue = "1";

    private readonly List<IDisposable> _pageResources = new();
    private readonly Dictionary<double, XFont> _fontCache = new();

    public (int WidthPx, int HeightPx) GetImagePixelSize(byte[] jpegBytes)
    {
        var info = Image.Identify(jpegBytes);
        return (info.Width, info.Height);
    }

    public int AddPage(PdfDocument document, byte[] jpegBytes, IReadOnlyList<TextBoxResponse> ocrResults, double dpi = 200)
    {
        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);

        var memStreamImg = new MemoryStream(jpegBytes);
        var xImage = XImage.FromStream(() => memStreamImg);
        // Giữ sống tới khi document.Save() chạy xong — KHÔNG dispose ngay ở cuối trang này.
        // XImage.FromStream(Func<Stream>) dùng factory trễ để PdfSharpCore có thể nhúng thẳng bytes
        // JPEG gốc (passthrough) lúc Save(); factory đó có thể bị gọi lại cho các trang trước sau
        // khi vòng lặp của caller đã qua trang đó — dispose sớm gây ObjectDisposedException hoặc
        // ảnh trống/hỏng cho các trang trước trang cuối. Caller phải Dispose() builder sau Save().
        _pageResources.Add(memStreamImg);
        _pageResources.Add(xImage);

        double scale = 72.0 / dpi;
        page.Width = xImage.PixelWidth * scale;
        page.Height = xImage.PixelHeight * scale;

        int drawnCount = 0;

        // Vẽ text TRƯỚC — ảnh opaque vẽ SAU sẽ phủ kín toàn trang, khiến text vô hình tuyệt đối
        // với mắt người nhưng vẫn trích xuất/tìm kiếm được, không cần brush alpha thấp như trước.
        foreach (var boxData in ocrResults)
        {
            if (boxData.Box == null || boxData.Box.Count != 4) continue;
            if (string.IsNullOrWhiteSpace(boxData.Text)) continue;

            double x0 = boxData.Box[0] * scale;
            double y0 = boxData.Box[1] * scale;
            double x1 = boxData.Box[2] * scale;
            double y1 = boxData.Box[3] * scale;

            double w = Math.Max(x1 - x0, 10 * scale);
            double h = Math.Max(y1 - y0, 6 * scale);

            var lines = boxData.Text.Replace("\r\n", "\n").Split('\n');
            double lineHeight = h / lines.Length;
            bool drewAnyLine = false;

            for (int li = 0; li < lines.Length; li++)
            {
                var line = lines[li];
                if (string.IsNullOrWhiteSpace(line)) continue;

                double fontSize = Math.Max(4, lineHeight * 0.75);
                var font = GetOrCreateFont(fontSize);

                // Co/dãn fontSize theo chiều rộng thực đo được, để vùng highlight khi search khớp
                // đúng với bề ngang chữ trên ảnh thay vì chỉ suy từ chiều cao box như trước đây.
                double measuredWidth = gfx.MeasureString(line, font).Width;
                if (measuredWidth > 0.01 && w > 0)
                {
                    double fitted = fontSize * (w / measuredWidth);
                    fontSize = Math.Clamp(fitted, 4, Math.Max(4, lineHeight * 1.5));
                    font = GetOrCreateFont(fontSize);
                }

                var lineRect = new XRect(x0, y0 + li * lineHeight, w, lineHeight);
                gfx.DrawString(line, font, XBrushes.Black, lineRect, XStringFormats.TopLeft);
                drewAnyLine = true;
            }

            if (drewAnyLine) drawnCount++;
        }

        // Ảnh vẽ SAU CÙNG, phủ kín trang, che toàn bộ lớp text đã vẽ ở trên.
        gfx.DrawImage(xImage, 0, 0, page.Width, page.Height);

        return drawnCount;
    }

    public void MarkAsSearchable(PdfDocument document)
    {
        document.Info.Elements.SetString(SearchableMarkerKey, SearchableMarkerValue);
    }

    public bool IsAlreadySearchable(byte[] pdfBytes)
    {
        using var reader = PdfReader.Open(new MemoryStream(pdfBytes), PdfDocumentOpenMode.InformationOnly);
        return reader.Info.Elements.ContainsKey(SearchableMarkerKey);
    }

    private XFont GetOrCreateFont(double fontSize)
    {
        var key = Math.Round(fontSize, 1);
        if (!_fontCache.TryGetValue(key, out var font))
        {
            font = new XFont("Open Sans", key, XFontStyle.Regular);
            _fontCache[key] = font;
        }
        return font;
    }

    public void Dispose()
    {
        foreach (var resource in _pageResources)
        {
            resource.Dispose();
        }
        _pageResources.Clear();
        _fontCache.Clear();
    }
}
