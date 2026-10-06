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

// ================= CHUYỂN ẢNH -> PDF (DocumentProcessingOptions.ConvertImageToPdf) =================
var convert = new DocumentProcessingOptions(ConvertImageToPdf: true);

// ---- 5. JPEG thường -> PDF 1 trang, MIME/đuôi đổi ----
{
    using var input = new MemoryStream(bigImage);
    var result = await service.CompressAsync(input, "anh_scan_lon.jpg", "image/jpeg", options: convert);
    var bytes = ((MemoryStream)result.Stream).ToArray();
    using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
    var pg = pdf.GetPage(1);
    await Check("JPEG -> PDF: MIME application/pdf, đuôi .pdf, ConvertedFromImage",
        result.MimeType == "application/pdf" && result.FileName == "anh_scan_lon.pdf" && result.ConvertedFromImage,
        $"{result.FileName} {result.MimeType}");
    await Check("JPEG -> PDF: đúng 1 trang, khổ = px*72/150 (3000x2400 -> 1754x1403 px)",
        pdf.NumberOfPages == 1 && Math.Abs(pg.Width - 1754 * 72.0 / 150) < 1.5,
        $"pages={pdf.NumberOfPages}, {pg.Width:F1}x{pg.Height:F1}pt");
    await Check("JPEG -> PDF: PDFtoImage mở lại & đếm được 1 trang", PDFtoImage.Conversion.GetPageCount(bytes) == 1, "ok");
}

// ---- 6. Ảnh nhỏ KHÔNG bị bỏ qua dù bản PDF lớn hơn ảnh gốc ----
{
    using var input = new MemoryStream(smallImage);
    var result = await service.CompressAsync(input, "anh_nho.jpg", "image/jpeg", options: convert);
    await Check("Ảnh nhỏ vẫn được chuyển PDF (bỏ qua quy tắc 'không nhỏ hơn thì giữ gốc')",
        result.ConvertedFromImage && result.MimeType == "application/pdf", $"{smallImage.Length} -> {result.Size} bytes");
}

// ---- 7. EXIF orientation 6 (ảnh ngang 1200x600 chụp xoay) -> trang phải DỌC ----
{
    byte[] exifJpeg;
    using (var img = new Image<Rgba32>(1200, 600))
    {
        img.Metadata.ExifProfile = new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();
        img.Metadata.ExifProfile.SetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation, (ushort)6);
        using var ms = new MemoryStream();
        img.SaveAsJpeg(ms);
        exifJpeg = ms.ToArray();
    }
    using var input = new MemoryStream(exifJpeg);
    var result = await service.CompressAsync(input, "chup_dien_thoai.jpg", "image/jpeg", options: convert);
    using var pdf = UglyToad.PdfPig.PdfDocument.Open(((MemoryStream)result.Stream).ToArray());
    var pg = pdf.GetPage(1);
    await Check("EXIF orientation=6 được xoay thật (trang dọc)", pg.Height > pg.Width, $"{pg.Width:F0}x{pg.Height:F0}pt");
}

// ---- 8. PNG có alpha trong suốt -> nền TRẮNG (không đen) ----
{
    byte[] pngAlpha;
    using (var img = new Image<Rgba32>(400, 300, new Rgba32(0, 0, 0, 0)))
    {
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        pngAlpha = ms.ToArray();
    }
    using var input = new MemoryStream(pngAlpha);
    var result = await service.CompressAsync(input, "trong_suot.png", "image/png", options: convert);
    var pdfBytes = ((MemoryStream)result.Stream).ToArray();
    using var rendered = new MemoryStream();
    PDFtoImage.Conversion.SavePng(rendered, pdfBytes, page: 0);
    rendered.Position = 0;
    using var back = Image.Load<Rgba32>(rendered);
    var px = back[back.Width / 2, back.Height / 2];
    await Check("PNG alpha: nền trắng sau khi sang PDF", px.R > 240 && px.G > 240 && px.B > 240, $"pixel giữa = {px}");
}

// ---- 9. TIFF nhiều trang (3 trang) -> PDF 3 trang ----
{
    var tiff = BuildMultiPageTiff(pages: 3, width: 800, height: 600);
    using var input = new MemoryStream(tiff);
    var result = await service.CompressAsync(input, "scan_3_trang.tif", "image/tiff", options: convert);
    var pdfBytes = ((MemoryStream)result.Stream).ToArray();
    await Check("TIFF 3 trang -> PDF 3 trang", PDFtoImage.Conversion.GetPageCount(pdfBytes) == 3 && result.FileName == "scan_3_trang.pdf",
        $"pages={PDFtoImage.Conversion.GetPageCount(pdfBytes)}, {result.FileName}");
}

// ---- 10. Ảnh hỏng / quá khổ -> DocumentConversionException (fail-hard, KHÔNG fallback) ----
{
    var garbage = new byte[2048];
    new Random(1).NextBytes(garbage);
    using var input = new MemoryStream(garbage);
    var threw = false;
    try { await service.CompressAsync(input, "hong.jpg", "image/jpeg", options: convert); }
    catch (DocumentConversionException ex) { threw = true; Console.WriteLine("   thông điệp: " + ex.Message); }
    await Check("Ảnh hỏng -> ném DocumentConversionException", threw, "");
}
{
    using var huge = new Image<Rgba32>(7000, 7000); // 49MP > 40MP
    using var ms = new MemoryStream();
    huge.SaveAsPng(ms);
    using var input = new MemoryStream(ms.ToArray());
    var threw = false;
    try { await service.CompressAsync(input, "qua_lon.png", "image/png", options: convert); }
    catch (DocumentConversionException) { threw = true; }
    await Check("Ảnh > 40MP -> ném DocumentConversionException", threw, "");
}

// ---- 11. Hồi quy: không bật cờ -> hành vi cũ (ảnh vẫn là ảnh); PDF điện tử không bị đụng ----
{
    using var input = new MemoryStream(bigImage);
    var result = await service.CompressAsync(input, "x.jpg", "image/jpeg");
    await Check("Không bật cờ: ảnh vẫn là ảnh", result.MimeType == "image/jpeg" && !result.ConvertedFromImage, result.MimeType);
}
{
    using var input = new MemoryStream(BuildTextPdf());
    var result = await service.CompressAsync(input, "dt.pdf", "application/pdf", options: convert);
    await Check("Bật cờ: PDF điện tử vẫn giữ nguyên", !result.WasCompressed && result.MimeType == "application/pdf", "");
}

// ---- 12. Luồng chunked: tên đã đổi .pdf + MIME pdf nhưng bytes là ẢNH -> vẫn nhận diện & chuyển đổi ----
{
    using var input = new MemoryStream(bigImage);
    var result = await service.CompressAsync(input, "chup.pdf", "application/pdf", options: convert);
    var bytes = ((MemoryStream)result.Stream).ToArray();
    await Check("Chunked: ảnh mang tên .pdf vẫn được chuyển thành PDF thật",
        result.ConvertedFromImage && bytes.Length > 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && PDFtoImage.Conversion.GetPageCount(bytes) == 1,
        $"converted={result.ConvertedFromImage}, {bytes.Length} bytes");
}

// ---- 13. Đua: DB nói image/jpeg nhưng bytes đã là PDF -> không ném lỗi, giữ nguyên PDF ----
{
    var pdfBytes = BuildTextPdf();
    using var input = new MemoryStream(pdfBytes);
    var result = await service.CompressAsync(input, "x.jpg", "image/jpeg", options: convert);
    await Check("Metadata ảnh nhưng bytes PDF -> không convert, không lỗi", !result.ConvertedFromImage && result.MimeType == "application/pdf", result.MimeType);
}

// ---- 14. File text bắt đầu bằng "BM" KHÔNG bị nhận nhầm là BMP ----
{
    var text = System.Text.Encoding.UTF8.GetBytes("BM-2026 bien ban ghi chu, khong phai anh bitmap.");
    using var input = new MemoryStream(text);
    var result = await service.CompressAsync(input, "ghi_chu.txt", "text/plain", options: convert);
    await Check("Text bắt đầu bằng 'BM' giữ nguyên, không bị coi là ảnh", !result.ConvertedFromImage && result.MimeType == "text/plain", result.MimeType);
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

static byte[] BuildMultiPageTiff(int pages, int width, int height)
{
    using var ms = new MemoryStream();
    using (var tif = BitMiracle.LibTiff.Classic.Tiff.ClientOpen("out", "w", ms, new BitMiracle.LibTiff.Classic.TiffStream()))
    {
        for (var p = 0; p < pages; p++)
        {
            tif.SetField(BitMiracle.LibTiff.Classic.TiffTag.IMAGEWIDTH, width);
            tif.SetField(BitMiracle.LibTiff.Classic.TiffTag.IMAGELENGTH, height);
            tif.SetField(BitMiracle.LibTiff.Classic.TiffTag.SAMPLESPERPIXEL, 3);
            tif.SetField(BitMiracle.LibTiff.Classic.TiffTag.BITSPERSAMPLE, 8);
            tif.SetField(BitMiracle.LibTiff.Classic.TiffTag.ORIENTATION, BitMiracle.LibTiff.Classic.Orientation.TOPLEFT);
            tif.SetField(BitMiracle.LibTiff.Classic.TiffTag.PLANARCONFIG, BitMiracle.LibTiff.Classic.PlanarConfig.CONTIG);
            tif.SetField(BitMiracle.LibTiff.Classic.TiffTag.PHOTOMETRIC, BitMiracle.LibTiff.Classic.Photometric.RGB);
            tif.SetField(BitMiracle.LibTiff.Classic.TiffTag.ROWSPERSTRIP, height);
            var row = new byte[width * 3];
            for (var x = 0; x < width; x++) { row[x * 3] = (byte)(p * 80); row[x * 3 + 1] = 120; row[x * 3 + 2] = 200; }
            for (var y = 0; y < height; y++) tif.WriteScanline(row, y);
            tif.WriteDirectory();
        }
    }
    return ms.ToArray();
}
