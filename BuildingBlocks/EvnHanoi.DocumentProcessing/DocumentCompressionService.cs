using Microsoft.Extensions.Logging;

namespace EvnHanoi.DocumentProcessing;

/// <summary>
/// Nén tài liệu tải lên (đồng bộ, ngay lúc upload) về mức tương đương ~150 DPI cho PDF scan và
/// file ảnh — giữ nguyên PDF điện tử gốc (có lớp text/vector thật). Chỉ giữ 1 bản (đã nén hoặc
/// gốc nếu không áp dụng/nén thất bại) — không lưu song song 2 bản.
/// </summary>
public interface IDocumentCompressionService
{
    Task<DocumentCompressionResult> CompressAsync(
        Stream inputStream,
        string fileName,
        string mimeType,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentCompressionResult
{
    public required Stream Stream { get; init; }
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
    public required long Size { get; init; }
    public required bool WasCompressed { get; init; }
}

public class DocumentCompressionService : IDocumentCompressionService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"
    };

    // TIFF/BMP không nén hoặc nén yếu -> re-encode JPEG khi nén để giảm thêm dung lượng, đồng nghĩa
    // phải đổi đuôi file tương ứng. JPEG/PNG giữ nguyên định dạng gốc.
    private static readonly HashSet<string> RasterOnlyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".tif", ".tiff"
    };

    private readonly ILogger<DocumentCompressionService> _logger;

    public DocumentCompressionService(ILogger<DocumentCompressionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DocumentCompressionResult> CompressAsync(
        Stream inputStream,
        string fileName,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        if (inputStream.CanSeek)
            inputStream.Seek(0, SeekOrigin.Begin);

        using var buffer = new MemoryStream();
        await inputStream.CopyToAsync(buffer, cancellationToken);
        var originalBytes = buffer.ToArray();

        if (inputStream.CanSeek)
            inputStream.Seek(0, SeekOrigin.Begin);

        var extension = Path.GetExtension(fileName);
        var isPdf = IsPdf(extension, mimeType);
        var isImage = !isPdf && IsImage(extension, mimeType);

        if (!isPdf && !isImage || originalBytes.Length == 0)
            return Unchanged(originalBytes, fileName, mimeType);

        try
        {
            byte[] candidateBytes;
            string candidateFileName;
            string candidateMimeType;

            if (isPdf)
            {
                var classification = PdfTextLayerDetector.Classify(originalBytes);
                if (classification != PdfTextLayerDetector.Classification.Scanned)
                {
                    _logger.LogInformation(
                        "Bỏ qua nén PDF {FileName}: phân loại {Classification} (không phải scan thuần).",
                        fileName, classification);
                    return Unchanged(originalBytes, fileName, mimeType);
                }

                candidateBytes = ScannedPdfRasterizer.Rasterize(originalBytes);
                candidateFileName = fileName;
                candidateMimeType = "application/pdf";
            }
            else
            {
                candidateBytes = ImageDownsampler.Downsample(originalBytes, out var outputMimeType);
                candidateFileName = RasterOnlyExtensions.Contains(extension)
                    ? Path.ChangeExtension(fileName, ".jpg")
                    : fileName;
                candidateMimeType = outputMimeType;
            }

            // Không có gì đảm bảo bản nén luôn nhỏ hơn bản gốc (PDF/ảnh vốn đã tối ưu sẵn, ảnh vốn đã
            // nhỏ hơn khổ A4 giả định...) — nếu không nhỏ hơn thì giữ nguyên bản gốc, đúng tinh thần
            // "giảm dung lượng" thay vì áp DPI/định dạng mới một cách mù quáng.
            if (candidateBytes.LongLength >= originalBytes.LongLength)
            {
                _logger.LogInformation(
                    "Bỏ qua kết quả nén {FileName}: bản nén ({CompressedSize} bytes) không nhỏ hơn bản gốc ({OriginalSize} bytes).",
                    fileName, candidateBytes.LongLength, originalBytes.LongLength);
                return Unchanged(originalBytes, fileName, mimeType);
            }

            _logger.LogInformation(
                "Đã nén {FileName}: {OriginalSize} bytes -> {CompressedSize} bytes.",
                fileName, originalBytes.LongLength, candidateBytes.LongLength);

            return new DocumentCompressionResult
            {
                Stream = new MemoryStream(candidateBytes),
                FileName = candidateFileName,
                MimeType = candidateMimeType,
                Size = candidateBytes.LongLength,
                WasCompressed = true
            };
        }
        catch (Exception ex)
        {
            // Nén thất bại (PDF hỏng, ảnh dị dạng, vượt ngưỡng an toàn decompression-bomb...) -> fallback
            // lưu file gốc, không chặn cả request upload chỉ vì tính năng tối ưu dung lượng.
            _logger.LogWarning(ex, "Nén file thất bại, fallback lưu bản gốc: {FileName}", fileName);
            return Unchanged(originalBytes, fileName, mimeType);
        }
    }

    private static DocumentCompressionResult Unchanged(byte[] originalBytes, string fileName, string mimeType) =>
        new()
        {
            Stream = new MemoryStream(originalBytes),
            FileName = fileName,
            MimeType = mimeType,
            Size = originalBytes.LongLength,
            WasCompressed = false
        };

    private static bool IsImage(string extension, string? mimeType)
    {
        if (!string.IsNullOrWhiteSpace(mimeType) && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrEmpty(extension) && ImageExtensions.Contains(extension);
    }

    private static bool IsPdf(string extension, string? mimeType)
    {
        if (!string.IsNullOrWhiteSpace(mimeType) && mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return true;

        return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }
}
