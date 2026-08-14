using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace EvnHanoi.DocumentProcessing;

/// <summary>
/// Khử nhiễu PDF theo yêu cầu người dùng — render lại từng trang thành ảnh, cân bằng histogram
/// (sáng/tương phản) toàn cục, rồi nhúng lại thành 1 PDF mới. Khác với <see cref="ScannedPdfRasterizer"/>
/// (chạy có điều kiện, âm thầm, chỉ cho PDF scan thuần lúc upload), đây là tác vụ người dùng chủ động
/// bấm nút để chạy trên PDF hiện tại, không phân loại scan/text trước.
/// </summary>
public interface INoiseReductionService
{
    Task<byte[]> ApplyAsync(byte[] pdfBytes, CancellationToken cancellationToken = default);
}

public class NoiseReductionService : INoiseReductionService
{
    internal const int TargetDpi = 150;
    internal const int MaxPageCount = 30;

    public Task<byte[]> ApplyAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        var pageCount = PDFtoImage.Conversion.GetPageCount(pdfBytes);
        if (pageCount <= 0)
            throw new InvalidOperationException("PDF không có trang nào để xử lý.");

        if (pageCount > MaxPageCount)
            throw new InvalidOperationException(
                $"Tài liệu có {pageCount} trang, vượt quá giới hạn {MaxPageCount} trang cho khử nhiễu đồng bộ. " +
                "Vui lòng chia nhỏ tài liệu hoặc dùng chức năng tải phiên bản mới thủ công.");

        using var outDocument = new PdfDocument();

        // Giữ sống các stream ảnh trang tới sau Save() — XImage.FromStream dùng factory trễ,
        // có thể được PdfSharpCore gọi lại cho trang trước sau khi vòng lặp đã đi qua (xem cùng
        // lưu ý trong ScannedPdfRasterizer.Rasterize).
        var pageStreams = new List<MemoryStream>(pageCount);
        try
        {
            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var renderStream = new MemoryStream();
                var renderOptions = new PDFtoImage.RenderOptions { Dpi = TargetDpi, WithAnnotations = true };
                PDFtoImage.Conversion.SaveJpeg(renderStream, pdfBytes, password: null, page: pageIndex, options: renderOptions);

                if (renderStream.Length == 0)
                    throw new InvalidOperationException($"Render trang {pageIndex + 1}/{pageCount} ra JPEG rỗng.");

                renderStream.Position = 0;
                using var image = Image.Load<Rgba32>(renderStream);
                image.Mutate(x => x.HistogramEqualization());

                var pageStream = new MemoryStream();
                image.SaveAsJpeg(pageStream, new JpegEncoder { Quality = 85 });
                pageStream.Position = 0;
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
            return Task.FromResult(outStream.ToArray());
        }
        finally
        {
            foreach (var pageStream in pageStreams)
                pageStream.Dispose();
        }
    }
}
