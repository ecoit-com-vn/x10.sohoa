using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Core.Services;

/// <summary>
/// Service cho validate MIME type dựa trên UPLOAD_CONFIG table
/// </summary>
public interface IMimeTypeValidationService
{
    /// <summary>
    /// Check xem MIME type có được phép không
    /// </summary>
    Task<bool> IsAllowedMimeTypeAsync(string mimeType);

    /// <summary>
    /// Validate magic bytes (file signature) để chắc chắn file type đúng
    /// </summary>
    bool ValidateMagicBytes(Stream fileStream, string mimeType);
}

public class MimeTypeValidationService : IMimeTypeValidationService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<MimeTypeValidationService> _logger;
    private Dictionary<string, byte[]>? _mimeTypeCache;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private const int CACHE_MINUTES = 60;

    public MimeTypeValidationService(IDocumentRepository documentRepository, ILogger<MimeTypeValidationService> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsAllowedMimeTypeAsync(string mimeType)
    {
        try
        {
            // For MVP: hardcode a basic whitelist (since UPLOAD_CONFIG doesn't exist yet)
            // In production: load from UPLOAD_CONFIG table
            var allowedTypes = new[]
            {
                "application/pdf",
                "application/msword",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/vnd.ms-excel",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "image/jpeg",
                "image/png",
                "image/tiff",
                "application/vnd.dwg",
                "image/x-dwg"
            };

            bool isAllowed = allowedTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase);
            
            if (!isAllowed)
                _logger.LogWarning("MIME type not allowed: {MimeType}", mimeType);

            return isAllowed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating MIME type: {MimeType}", mimeType);
            return false;
        }
    }

    public bool ValidateMagicBytes(Stream fileStream, string mimeType)
    {
        try
        {
            byte[] header = new byte[512];
            fileStream.Read(header, 0, Math.Min(512, (int)fileStream.Length));
            fileStream.Seek(0, SeekOrigin.Begin);  // Reset stream position

            return ValidateMagicBytesInternal(header, mimeType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating magic bytes for {MimeType}", mimeType);
            return false;
        }
    }

    private bool ValidateMagicBytesInternal(byte[] header, string mimeType)
    {
        // Map common MIME types to file signatures (magic bytes)
        if (mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return header.Length >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46;  // %PDF

        if (mimeType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
            return header.Length >= 2 && header[0] == 0xFF && header[1] == 0xD8;  // FFD8

        if (mimeType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
            return header.Length >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;  // PNG

        if (mimeType.Equals("image/tiff", StringComparison.OrdinalIgnoreCase))
            return (header.Length >= 4 && header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) ||  // Little-endian
                   (header.Length >= 4 && header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A);   // Big-endian

        if (mimeType.Equals("application/msword", StringComparison.OrdinalIgnoreCase) ||
            mimeType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase))
            return header.Length >= 4 && header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0;  // OLE/OOXML

        if (mimeType.Equals("application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase) ||
            mimeType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase))
            return header.Length >= 4 && header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0;  // OLE/OOXML

        // For DWG files - AutoCAD format has "AC" prefix + version
        if (mimeType.Equals("application/vnd.dwg", StringComparison.OrdinalIgnoreCase) ||
            mimeType.Equals("image/x-dwg", StringComparison.OrdinalIgnoreCase))
            return header.Length >= 2 && header[0] == 0x41 && header[1] == 0x43;  // AC

        // Unknown MIME type - log warning but allow (not strictly enforced)
        _logger.LogWarning("Unknown MIME type for magic bytes validation: {MimeType}", mimeType);
        return true;
    }
}
