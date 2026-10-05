using BitMiracle.LibTiff.Classic;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace EvnHanoi.DocumentProcessing;

/// <summary>
/// Chuyển file ảnh (jpg/png/bmp/tiff, kể cả TIFF nhiều trang) thành PDF ảnh thuần, mỗi frame = 1 trang,
/// ở mức ~150 DPI — cùng quy ước khổ trang với <see cref="ScannedPdfRasterizer"/> (trang = px × 72/150) để
/// OcrWorker render lại ở OcrSourceDpi ra đúng số pixel, toạ độ box OCR không lệch.
/// ImageSharp 1.0.4 không đọc được TIFF → TIFF được giải mã bằng LibTiff.NET (managed) sang Rgba32.
/// </summary>
internal static class ImageToPdfConverter
{
    internal const int TargetDpi = 150;
    internal const int JpegQuality = 85;

    // Giới hạn riêng cho đường chuyển đổi (thấp hơn MaxSafePixelCount của ImageDownsampler: ảnh 100MP ≈
    // 400MB RAM RGBA, nhiều upload đồng thời có thể OOM pod).
    internal const long MaxPixelsPerFrame = 40_000_000L;
    internal const long MaxTotalPixels = 80_000_000L;
    internal const int MaxFrames = 200;

    private const string UnreadableMessage = "Không đọc được file ảnh (định dạng không hợp lệ hoặc file bị hỏng).";

    internal static byte[] Convert(byte[] imageBytes)
    {
        var frames = new List<Image<Rgba32>>();
        var pageStreams = new List<MemoryStream>();
        try
        {
            if (IsTiff(imageBytes))
                DecodeTiff(imageBytes, frames);
            else
                DecodeWithImageSharp(imageBytes, frames);

            if (frames.Count == 0)
                throw new DocumentConversionException(UnreadableMessage);

            using var outDocument = new PdfDocument();

            foreach (var frame in frames)
            {
                var longEdge = Math.Max(frame.Width, frame.Height);
                var targetLongEdge = ImageDownsampler.ResolveTargetLongEdge(frame.Metadata, longEdge);
                if (longEdge > targetLongEdge)
                {
                    var scale = (double)targetLongEdge / longEdge;
                    var w = Math.Max(1, (int)Math.Round(frame.Width * scale));
                    var h = Math.Max(1, (int)Math.Round(frame.Height * scale));
                    frame.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(w, h), Sampler = KnownResamplers.Lanczos3, Mode = ResizeMode.Max }));
                }

                // Flatten alpha lên nền trắng (PNG trong suốt không được để thành nền đen khi sang JPEG).
                frame.Mutate(x => x.BackgroundColor(Color.White));

                using var jpegStream = new MemoryStream();
                frame.SaveAsJpeg(jpegStream, new JpegEncoder { Quality = JpegQuality });
                var pageStream = new MemoryStream(jpegStream.ToArray());
                pageStreams.Add(pageStream);

                // Factory trễ: PdfSharpCore nhúng bytes JPEG lúc Save() → không dispose stream trong vòng lặp.
                var xImage = XImage.FromStream(() => pageStream);
                var page = outDocument.AddPage();
                using var gfx = XGraphics.FromPdfPage(page);

                const double pt = 72.0 / TargetDpi;
                page.Width = xImage.PixelWidth * pt;
                page.Height = xImage.PixelHeight * pt;
                gfx.DrawImage(xImage, 0, 0, page.Width, page.Height);
            }

            using var outStream = new MemoryStream();
            outDocument.Save(outStream);
            return outStream.ToArray();
        }
        catch (DocumentConversionException) { throw; }
        catch (Exception ex)
        {
            throw new DocumentConversionException("Không chuyển được ảnh sang PDF (file ảnh có thể bị hỏng hoặc không được hỗ trợ).", ex);
        }
        finally
        {
            foreach (var f in frames) f.Dispose();
            foreach (var s in pageStreams) s.Dispose();
        }
    }

    private static bool IsTiff(byte[] b) =>
        b.Length >= 4 &&
        ((b[0] == 'I' && b[1] == 'I' && b[2] == 42 && b[3] == 0) ||
         (b[0] == 'M' && b[1] == 'M' && b[2] == 0 && b[3] == 42));

    private static void DecodeWithImageSharp(byte[] imageBytes, List<Image<Rgba32>> frames)
    {
        IImageInfo info;
        try { info = Image.Identify(imageBytes) ?? throw new DocumentConversionException(UnreadableMessage); }
        catch (DocumentConversionException) { throw; }
        catch (Exception ex) { throw new DocumentConversionException(UnreadableMessage, ex); }

        // Kiểm tra trước khi decode (chỉ đọc header) để chặn decompression-bomb.
        EnsureFrameWithinLimit(info.Width, info.Height);

        Image<Rgba32> image;
        try { image = Image.Load<Rgba32>(imageBytes); }
        catch (Exception ex) { throw new DocumentConversionException(UnreadableMessage, ex); }

        using (image)
        {
            // EXIF orientation: PdfSharpCore nhúng thẳng bytes JPEG và bỏ qua EXIF → ảnh chụp điện thoại
            // sẽ nằm ngang/ngược trong PDF. Xoay thật pixel rồi xoá EXIF.
            image.Mutate(x => x.AutoOrient());
            image.Metadata.ExifProfile = null;

            long total = 0;
            for (var i = 0; i < image.Frames.Count; i++)
                total += (long)image.Frames[i].Width * image.Frames[i].Height;
            EnsureTotalWithinLimit(image.Frames.Count, total);

            for (var i = 0; i < image.Frames.Count; i++)
                frames.Add(image.Frames.CloneFrame(i));
        }
    }

    private static void DecodeTiff(byte[] imageBytes, List<Image<Rgba32>> frames)
    {
        using var ms = new MemoryStream(imageBytes);
        using var tif = Tiff.ClientOpen("in-memory", "r", ms, new TiffStream())
            ?? throw new DocumentConversionException(UnreadableMessage);

        var dirCount = tif.NumberOfDirectories();
        if (dirCount > MaxFrames)
            throw new DocumentConversionException($"Ảnh có quá nhiều trang ({dirCount}); tối đa {MaxFrames}.");

        long total = 0;
        for (short d = 0; d < dirCount; d++)
        {
            if (!tif.SetDirectory(d))
                throw new DocumentConversionException(UnreadableMessage);

            var w = tif.GetField(TiffTag.IMAGEWIDTH)?[0].ToInt() ?? 0;
            var h = tif.GetField(TiffTag.IMAGELENGTH)?[0].ToInt() ?? 0;
            if (w <= 0 || h <= 0)
                throw new DocumentConversionException(UnreadableMessage);

            EnsureFrameWithinLimit(w, h);
            total += (long)w * h;
            EnsureTotalWithinLimit(dirCount, total);

            var raster = new int[w * h];
            if (!tif.ReadRGBAImageOriented(w, h, raster, Orientation.TOPLEFT))
                throw new DocumentConversionException(UnreadableMessage);

            // Ghi thẳng vào Image<Rgba32> (không qua mảng byte trung gian) để giảm đỉnh RAM ~1/3.
            var frame = new Image<Rgba32>(w, h);
            frames.Add(frame);
            for (var y = 0; y < h; y++)
            {
                var row = frame.GetPixelRowSpan(y);
                var offset = y * w;
                for (var x = 0; x < row.Length; x++)
                {
                    var v = raster[offset + x];
                    row[x] = new Rgba32((byte)Tiff.GetR(v), (byte)Tiff.GetG(v), (byte)Tiff.GetB(v), (byte)Tiff.GetA(v));
                }
            }
        }
    }

    private static void EnsureFrameWithinLimit(int width, int height)
    {
        if ((long)width * height > MaxPixelsPerFrame)
            throw new DocumentConversionException(
                $"Ảnh quá lớn ({width}x{height}px) — vượt ngưỡng xử lý an toàn. Vui lòng giảm kích thước ảnh rồi tải lại.");
    }

    private static void EnsureTotalWithinLimit(int frameCount, long totalPixels)
    {
        if (frameCount > MaxFrames || totalPixels > MaxTotalPixels)
            throw new DocumentConversionException(
                $"Ảnh có quá nhiều trang/khung hoặc quá lớn ({frameCount} khung) — vượt ngưỡng xử lý an toàn.");
    }
}
