using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace EvnHanoi.DocumentProcessing;

/// <summary>
/// Dựng lại PDF scan (ảnh thuần) thành PDF ảnh mới ở DPI thấp hơn (150) — mỗi trang gốc được
/// render lại thành 1 ảnh JPEG rồi nhúng làm 1 trang PDF mới, không có lớp text (vì đầu vào
/// vốn không có lớp text để giữ lại — đã được <see cref="PdfTextLayerDetector"/> xác nhận).
/// </summary>
internal static class ScannedPdfRasterizer
{
    internal const int TargetDpi = 150;

    internal static byte[] Rasterize(byte[] pdfBytes)
    {
        var pageCount = PDFtoImage.Conversion.GetPageCount(pdfBytes);
        if (pageCount <= 0)
            throw new InvalidOperationException("PDF không có trang nào để render.");

        using var outDocument = new PdfDocument();

        // XImage.FromStream(Func<Stream>) dùng factory trễ để PdfSharpCore nhúng thẳng bytes JPEG gốc
        // (passthrough) lúc Save() — factory có thể được gọi lại cho các trang trước sau khi vòng lặp
        // đã đi qua trang đó, nên KHÔNG được dispose từng stream ngay trong vòng lặp (xem cùng lưu ý
        // trong SearchablePdfBuilder.AddPage của DigitizationService) — phải giữ sống tới sau Save().
        var pageStreams = new List<MemoryStream>(pageCount);
        try
        {
            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                using var renderStream = new MemoryStream();
                var renderOptions = new PDFtoImage.RenderOptions { Dpi = TargetDpi, WithAnnotations = true };
                PDFtoImage.Conversion.SaveJpeg(renderStream, pdfBytes, password: null, page: pageIndex, options: renderOptions);

                var jpegBytes = renderStream.ToArray();
                if (jpegBytes.Length == 0)
                    throw new InvalidOperationException($"Render trang {pageIndex + 1}/{pageCount} ra JPEG rỗng.");

                var pageStream = new MemoryStream(jpegBytes);
                pageStreams.Add(pageStream);

                var xImage = XImage.FromStream(() => pageStream);
                var page = outDocument.AddPage();
                using var gfx = XGraphics.FromPdfPage(page);

                var scale = 72.0 / TargetDpi;
                page.Width = xImage.PixelWidth * scale;
                page.Height = xImage.PixelHeight * scale;
                gfx.DrawImage(xImage, 0, 0, page.Width, page.Height);
            }

            using var outStream = new MemoryStream();
            outDocument.Save(outStream);
            return outStream.ToArray();
        }
        finally
        {
            foreach (var pageStream in pageStreams)
                pageStream.Dispose();
        }
    }
}
