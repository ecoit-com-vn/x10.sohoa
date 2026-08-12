using EvnHanoi.DocumentProcessing;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<DocumentCompressionService>();
var service = new DocumentCompressionService(logger);

var failures = new List<string>();

async Task Check(string label, bool condition, string detail)
{
    Console.WriteLine($"[{(condition ? "OK" : "FAIL")}] {label} — {detail}");
    if (!condition) failures.Add(label);
    await Task.CompletedTask;
}

// ---- 1. Scanned PDF (ảnh thuần, KHÔNG có text) — kỳ vọng: bị rasterize, nhỏ hơn bản gốc ----
byte[] scannedPdf = BuildImageOnlyPdf(pageCount: 3, pageWidthPx: 1654, pageHeightPx: 2339); // A4 @ 200dpi giả lập
{
    using var input = new MemoryStream(scannedPdf);
    var result = await service.CompressAsync(input, "scan_don_thuan.pdf", "application/pdf");
    var bytes = ((MemoryStream)result.Stream).ToArray();
    await Check("Scanned PDF được nén", result.WasCompressed, $"{scannedPdf.Length} -> {bytes.Length} bytes");
    await Check("Scanned PDF vẫn là PDF hợp lệ (mở lại được, đủ số trang)",
        PDFtoImage.Conversion.GetPageCount(bytes) == 3,
        $"GetPageCount = {PDFtoImage.Conversion.GetPageCount(bytes)}");
}

// ---- 2. PDF điện tử gốc (có text thật) — kỳ vọng: giữ nguyên, KHÔNG rasterize ----
byte[] bornDigitalPdf = BuildTextPdf();
{
    using var input = new MemoryStream(bornDigitalPdf);
    var result = await service.CompressAsync(input, "hop_dong_dien_tu.pdf", "application/pdf");
    var bytes = ((MemoryStream)result.Stream).ToArray();
    await Check("PDF điện tử gốc KHÔNG bị nén (giữ nguyên bytes)",
        !result.WasCompressed && bytes.Length == bornDigitalPdf.Length,
        $"WasCompressed={result.WasCompressed}, {bornDigitalPdf.Length} -> {bytes.Length} bytes");
}

// ---- 3. Ảnh JPEG lớn, không có DPI metadata — kỳ vọng: giảm kích thước về ~1754px cạnh dài ----
byte[] bigImage = BuildJpeg(width: 3000, height: 2400);
{
    using var input = new MemoryStream(bigImage);
    var result = await service.CompressAsync(input, "anh_scan_lon.jpg", "image/jpeg");
    var bytes = ((MemoryStream)result.Stream).ToArray();
    var outInfo = Image.Identify(bytes);
    var longEdge = Math.Max(outInfo!.Width, outInfo.Height);
    await Check("Ảnh lớn được nén (dung lượng nhỏ hơn)", result.WasCompressed && bytes.Length < bigImage.Length,
        $"{bigImage.Length} -> {bytes.Length} bytes");
    await Check("Ảnh được resize về ~1754px cạnh dài (giả định A4 @150dpi)", longEdge <= 1754,
        $"Kích thước sau nén: {outInfo.Width}x{outInfo.Height}");
}

// ---- 4. Ảnh nhỏ (đã dưới ngưỡng) — kỳ vọng: không nén / giữ nguyên ----
byte[] smallImage = BuildJpeg(width: 800, height: 600);
{
    using var input = new MemoryStream(smallImage);
    var result = await service.CompressAsync(input, "anh_nho.jpg", "image/jpeg");
    var bytes = ((MemoryStream)result.Stream).ToArray();
    await Check("Ảnh đã nhỏ sẵn không bị 'nén phồng' lên", bytes.Length <= smallImage.Length * 1.05,
        $"WasCompressed={result.WasCompressed}, {smallImage.Length} -> {bytes.Length} bytes");
}

Console.WriteLine();
Console.WriteLine(failures.Count == 0
    ? "=== TẤT CẢ KIỂM TRA ĐỀU PASS ==="
    : $"=== CÓ {failures.Count} KIỂM TRA FAIL: {string.Join(", ", failures)} ===");

return failures.Count == 0 ? 0 : 1;

static byte[] BuildImageOnlyPdf(int pageCount, int pageWidthPx, int pageHeightPx)
{
    using var document = new PdfDocument();
    var pageStreams = new List<MemoryStream>();
    try
    {
        for (var i = 0; i < pageCount; i++)
        {
            var jpegBytes = BuildJpeg(pageWidthPx, pageHeightPx);
            var pageStream = new MemoryStream(jpegBytes);
            pageStreams.Add(pageStream);

            var xImage = XImage.FromStream(() => pageStream);
            var page = document.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);
            const double dpi = 200.0;
            page.Width = xImage.PixelWidth * 72.0 / dpi;
            page.Height = xImage.PixelHeight * 72.0 / dpi;
            gfx.DrawImage(xImage, 0, 0, page.Width, page.Height);
            // Cố tình KHÔNG gfx.DrawString(...) — mô phỏng PDF scan thuần, không có lớp text.
        }

        using var outStream = new MemoryStream();
        document.Save(outStream);
        return outStream.ToArray();
    }
    finally
    {
        foreach (var s in pageStreams) s.Dispose();
    }
}

static byte[] BuildTextPdf()
{
    using var document = new PdfDocument();
    var page = document.AddPage();
    using var gfx = XGraphics.FromPdfPage(page);
    var font = new XFont("Arial", 14, XFontStyle.Regular);
    gfx.DrawString(
        "Đây là văn bản điện tử gốc, có lớp text thật trích xuất được bằng PdfPig. " +
        "Hợp đồng số 123/HĐ-2026 giữa các bên liên quan, không phải bản scan.",
        font, XBrushes.Black, new XRect(40, 40, page.Width - 80, 200), XStringFormats.TopLeft);

    using var outStream = new MemoryStream();
    document.Save(outStream);
    return outStream.ToArray();
}

static byte[] BuildJpeg(int width, int height)
{
    using var image = new Image<Rgba32>(width, height);
    // Vẽ vài dải màu bằng cách set trực tiếp pixel (tránh phụ thuộc gói SixLabors.ImageSharp.Drawing
    // chỉ để tạo ảnh test) — đủ để ảnh không nén tầm thường về 0 byte, giống nội dung ảnh scan thật.
    for (var y = 0; y < image.Height; y++)
    {
        var shade = (byte)((y * 255 / Math.Max(1, image.Height - 1)) % 255);
        var color = new Rgba32(shade, (byte)(255 - shade), 128);
        var row = image.GetPixelRowSpan(y);
        for (var x = 0; x < row.Length; x++)
            row[x] = color;
    }

    using var ms = new MemoryStream();
    image.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
    return ms.ToArray();
}
