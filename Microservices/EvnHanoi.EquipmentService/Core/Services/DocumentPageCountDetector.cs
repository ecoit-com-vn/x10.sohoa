using UglyToad.PdfPig;

namespace EvnHanoi.EquipmentService.Core.Services;

/// <summary>
/// Đếm số trang file upload: PDF = số trang; ảnh = 1; còn lại = 0.
/// </summary>
public static class DocumentPageCountDetector
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp"
    };

    /// <summary>
    /// Đếm số trang từ stream (không dispose stream). Stream nên Seekable; vị trí sẽ được restore.
    /// </summary>
    public static int Detect(Stream content, string? fileName, string? mimeType)
    {
        if (content == null || !content.CanRead)
            return 0;

        if (IsImage(fileName, mimeType))
            return 1;

        if (!IsPdf(fileName, mimeType))
            return 0;

        long? originalPos = content.CanSeek ? content.Position : null;
        try
        {
            if (content.CanSeek)
                content.Seek(0, SeekOrigin.Begin);

            // Copy sang MemoryStream để PdfPig không ảnh hưởng stream gốc.
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            buffer.Position = 0;
            if (buffer.Length == 0)
                return 0;

            using var document = PdfDocument.Open(buffer);
            return Math.Max(0, document.NumberOfPages);
        }
        catch
        {
            return 0;
        }
        finally
        {
            if (originalPos.HasValue && content.CanSeek)
            {
                try { content.Seek(originalPos.Value, SeekOrigin.Begin); }
                catch { /* ignore */ }
            }
        }
    }

    private static bool IsImage(string? fileName, string? mimeType)
    {
        if (!string.IsNullOrWhiteSpace(mimeType)
            && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return true;

        var ext = Path.GetExtension(fileName ?? string.Empty);
        return !string.IsNullOrEmpty(ext) && ImageExtensions.Contains(ext);
    }

    private static bool IsPdf(string? fileName, string? mimeType)
    {
        if (!string.IsNullOrWhiteSpace(mimeType)
            && mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return true;

        return Path.GetExtension(fileName ?? string.Empty)
            .Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }
}
