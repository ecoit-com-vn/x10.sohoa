using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Processing;

namespace EvnHanoi.DocumentProcessing;

/// <summary>
/// Giảm kích thước ảnh scan (jpg/png/tiff/bmp) về mức tương đương ~150 DPI. File ảnh đơn lẻ
/// không có "kích thước trang" đáng tin cậy, nên quy tắc là: dùng DPI nhúng trong metadata nếu có
/// và lớn hơn 150; nếu không có, giả định khổ A4 và giới hạn cạnh dài ở mức tương đương 150 DPI.
/// </summary>
internal static class ImageDownsampler
{
    internal const double TargetDpi = 150.0;

    /// <summary>Cạnh dài tối đa giả định cho khổ A4 ở 150 DPI (150 * 11.7in ≈ 1754px), dùng khi
    /// ảnh không có DPI nhúng trong metadata (trường hợp phổ biến với ảnh chụp/scan qua web, app).</summary>
    internal const int MaxLongEdgePxAssumedA4 = 1754;

    /// <summary>Chặn decompression-bomb: từ chối nén (fallback lưu file gốc) nếu ảnh vượt ngưỡng này.</summary>
    internal const long MaxSafePixelCount = 100_000_000L;

    private const int JpegQuality = 78;

    internal static byte[] Downsample(byte[] originalBytes, out string outputMimeType)
    {
        var info = Image.Identify(originalBytes) ?? throw new InvalidOperationException("Không đọc được thông tin ảnh (header).");

        var pixelCount = (long)info.Width * info.Height;
        if (pixelCount > MaxSafePixelCount)
            throw new InvalidOperationException($"Ảnh vượt ngưỡng an toàn cho phép ({info.Width}x{info.Height}px).");

        var isPng = Image.DetectFormat(originalBytes) is PngFormat;

        using var image = Image.Load(originalBytes);

        var targetLongEdge = ResolveTargetLongEdge(image.Metadata, Math.Max(image.Width, image.Height));
        var currentLongEdge = Math.Max(image.Width, image.Height);

        if (currentLongEdge > targetLongEdge)
        {
            var scale = (double)targetLongEdge / currentLongEdge;
            var newWidth = Math.Max(1, (int)Math.Round(image.Width * scale));
            var newHeight = Math.Max(1, (int)Math.Round(image.Height * scale));

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(newWidth, newHeight),
                Sampler = KnownResamplers.Lanczos3,
                Mode = ResizeMode.Max
            }));
        }

        using var outStream = new MemoryStream();
        if (isPng)
        {
            // Giữ PNG cho ảnh PNG gốc (có thể có kênh alpha/trong suốt) — chỉ giảm kích thước pixel,
            // không ép sang JPEG để tránh mất transparency. TIFF/BMP/JPEG đều re-encode JPEG bên dưới.
            image.SaveAsPng(outStream);
            outputMimeType = "image/png";
        }
        else
        {
            image.SaveAsJpeg(outStream, new JpegEncoder { Quality = JpegQuality });
            outputMimeType = "image/jpeg";
        }

        return outStream.ToArray();
    }

    private static int ResolveTargetLongEdge(ImageMetadata metadata, int currentLongEdge)
    {
        var embeddedDpi = ResolveEmbeddedDpi(metadata);
        if (embeddedDpi.HasValue && embeddedDpi.Value > TargetDpi)
        {
            var ratio = TargetDpi / embeddedDpi.Value;
            return Math.Max(1, (int)Math.Round(currentLongEdge * ratio));
        }

        return MaxLongEdgePxAssumedA4;
    }

    private static double? ResolveEmbeddedDpi(ImageMetadata metadata)
    {
        var horizontal = metadata.HorizontalResolution;
        var vertical = metadata.VerticalResolution;
        if (horizontal <= 0 || vertical <= 0)
            return null;

        var dpi = Math.Max(horizontal, vertical);

        // Metadata có thể lưu theo pixel/cm hoặc pixel/mét thay vì pixel/inch (DPI) — quy đổi về DPI
        // để so sánh đúng với ngưỡng TargetDpi=150. Bỏ qua AspectRatio (không phải đơn vị mật độ).
        return metadata.ResolutionUnits switch
        {
            PixelResolutionUnit.PixelsPerCentimeter => dpi * 2.54,
            PixelResolutionUnit.PixelsPerMeter => dpi * 0.0254,
            PixelResolutionUnit.PixelsPerInch => dpi,
            _ => null
        };
    }
}
