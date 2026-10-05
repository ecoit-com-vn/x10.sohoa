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
        CancellationToken cancellationToken = default,
        DocumentProcessingOptions? options = null);
}

/// <summary>
/// Tuỳ chọn theo từng luồng gọi. <see cref="ConvertImageToPdf"/> = true: file ảnh được chuyển thành PDF
/// (đổi MIME → application/pdf, đuôi → .pdf) thay vì chỉ nén; lỗi chuyển đổi ném
/// <see cref="DocumentConversionException"/> (không fallback về ảnh gốc).
/// </summary>
public sealed record DocumentProcessingOptions(bool ConvertImageToPdf = false);

public sealed class DocumentCompressionResult
{
    public required Stream Stream { get; init; }
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
    public required long Size { get; init; }
    public required bool WasCompressed { get; init; }

    /// <summary>true nếu đầu vào là file ảnh đã được chuyển thành PDF.</summary>
    public bool ConvertedFromImage { get; init; }
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

    // Chuyển đổi ảnh → PDF tốn RAM (decode toàn ảnh) → giới hạn đồng thời trong 1 pod.
    private static readonly SemaphoreSlim ConversionGate = new(2, 2);

    /// <summary>Tên file có đuôi ảnh sẽ bị chuyển thành PDF khi bật <see cref="DocumentProcessingOptions.ConvertImageToPdf"/>
    /// — dùng để đổi tên SỚM ở luồng chunked (khi object key phải chốt trước lúc chuyển đổi).</summary>
    public static bool IsConvertibleImageFileName(string? fileName) =>
        !string.IsNullOrEmpty(fileName) && ImageExtensions.Contains(Path.GetExtension(fileName));

    private readonly ILogger<DocumentCompressionService> _logger;

    public DocumentCompressionService(ILogger<DocumentCompressionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DocumentCompressionResult> CompressAsync(
        Stream inputStream,
        string fileName,
        string mimeType,
        CancellationToken cancellationToken = default,
        DocumentProcessingOptions? options = null)
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

        // Luồng chunked đã đổi tên file thành .pdf từ lúc Initiate (object key chốt trước khi chuyển đổi)
        // → đuôi/MIME không còn đáng tin; khi bật chuyển đổi, nhận diện ảnh theo chữ ký byte thật.
        if (options?.ConvertImageToPdf == true)
        {
            if (!isImage && LooksLikeImage(originalBytes))
            {
                isImage = true;
                isPdf = false;
            }
            else if (isImage && LooksLikePdf(originalBytes))
            {
                // Metadata nói "ảnh" nhưng bytes đã là PDF (ví dụ đã được chuyển bởi một request khác).
                isImage = false;
                isPdf = true;
                mimeType = "application/pdf";
                // Đuôi cũng phải khớp nội dung (OcrWorker/ExtractionWorker suy tên JSON bằng cách cắt đuôi ".pdf").
                if (IsConvertibleImageFileName(fileName))
                    fileName = Path.ChangeExtension(fileName, ".pdf");
            }
        }

        if (!isPdf && !isImage || originalBytes.Length == 0)
            return Unchanged(originalBytes, fileName, mimeType);

        // Chuyển ảnh → PDF là bắt buộc (không theo quy tắc "nén không nhỏ hơn thì giữ gốc", không fallback).
        if (isImage && options?.ConvertImageToPdf == true)
            return await ConvertImageAsync(originalBytes, fileName, cancellationToken);

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

    private static bool LooksLikePdf(byte[] b) =>
        b.Length >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46;

    /// <summary>Nhận diện ảnh jpg/png/bmp/tiff theo chữ ký byte (không tin đuôi/MIME). Cần ≥ 10 byte đầu file.</summary>
    public static bool LooksLikeImage(byte[] b) =>
        b.Length >= 10 &&
        ((b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) ||                          // JPEG
         (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) ||          // PNG
         // BMP: "BM" + 4 byte kích thước + 4 byte reserved (luôn 0) — chặt hơn "BM" trần để file text bắt đầu bằng "BM" không bị nhận nhầm.
         (b[0] == (byte)'B' && b[1] == (byte)'M' && b[6] == 0 && b[7] == 0 && b[8] == 0 && b[9] == 0) ||
         (b[0] == (byte)'I' && b[1] == (byte)'I' && b[2] == 42 && b[3] == 0) ||     // TIFF LE
         (b[0] == (byte)'M' && b[1] == (byte)'M' && b[2] == 0 && b[3] == 42));      // TIFF BE

    private async Task<DocumentCompressionResult> ConvertImageAsync(
        byte[] originalBytes, string fileName, CancellationToken cancellationToken)
    {
        await ConversionGate.WaitAsync(cancellationToken);
        try
        {
            var pdfBytes = ImageToPdfConverter.Convert(originalBytes);
            _logger.LogInformation(
                "Đã chuyển ảnh {FileName} sang PDF: {OriginalSize} bytes -> {PdfSize} bytes.",
                fileName, originalBytes.LongLength, pdfBytes.LongLength);

            return new DocumentCompressionResult
            {
                Stream = new MemoryStream(pdfBytes),
                FileName = Path.ChangeExtension(fileName, ".pdf"),
                MimeType = "application/pdf",
                Size = pdfBytes.LongLength,
                WasCompressed = true,
                ConvertedFromImage = true
            };
        }
        catch (DocumentConversionException ex)
        {
            _logger.LogWarning(ex, "Chuyển ảnh sang PDF thất bại: {FileName}", fileName);
            throw;
        }
        finally
        {
            ConversionGate.Release();
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
