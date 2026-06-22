using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// API tạo download token cho file (EquipmentService)
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/files")]
public class FileDownloadTokenController : ControllerBase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _config;
    private readonly ILogger<FileDownloadTokenController> _logger;

    public FileDownloadTokenController(
        IDocumentRepository documentRepository,
        IDistributedCache cache,
        IConfiguration config,
        ILogger<FileDownloadTokenController> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? "system";

    private long GetUserUnitId()
    {
        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        return long.TryParse(unitIdClaim, out var unitId) ? unitId : 0;
    }

    /// <summary>
    /// Tạo download token cho file version
    /// </summary>
    [HttpGet("{versionId:guid}/download-url")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDownloadToken(
        [FromRoute] Guid versionId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get document version
            var version = await _documentRepository.GetDocumentVersionByIdAsync(versionId);
            if (version == null)
                return NotFound("Phiên bản tài liệu không tồn tại");

            if (version.IsDeleted)
                return NotFound("Phiên bản tài liệu đã bị xóa");

            // Get document to check folder and permission
            var document = await _documentRepository.GetDocumentByIdAsync(version.DocumentId);
            if (document == null)
                return NotFound("Tài liệu không tồn tại");

            if (document.IsDeleted)
                return NotFound("Tài liệu đã bị xóa");

            // Check permission theo đơn vị thư mục
            var userUnitId = GetUserUnitId();
            if (userUnitId == 0)
                return Unauthorized(new { code = "UNAUTHORIZED", message = "Không thể xác định đơn vị của người dùng" });

            if (document.FolderId.HasValue)
            {
                var folder = await _documentRepository.GetFolderByIdAsync(document.FolderId.Value);
                if (folder == null)
                    return NotFound(new { code = "NOT_FOUND", message = "Thư mục chứa tài liệu không tồn tại" });

                if (userUnitId < folder.UnitId)
                    return StatusCode(403, new { code = "FORBIDDEN", message = "Bạn không có quyền tải tài liệu trong thư mục này" });
            }

            if (string.IsNullOrEmpty(version.FilePath))
                return BadRequest(new { code = "VALIDATION_ERROR", message = "Tài liệu không có file" });

            // Generate one-time token
            var token = Guid.NewGuid().ToString("N");
            var ttlSeconds = _config.GetValue<int>("FileDownload:OneTimeTokenTTLSeconds", 60);

            // Store token metadata in Redis
            var tokenMetadata = new DownloadTokenMetadata
            {
                FilePath = version.FilePath,
                BucketName = _config["MinIO:DocumentBucketName"] ?? "documents",
                FileName = document.Name,
                MimeType = version.MimeType ?? "application/octet-stream"
            };

            var cacheKey = $"download:token:{token}";
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
            };

            var tokenJson = JsonSerializer.Serialize(tokenMetadata);
            await _cache.SetStringAsync(cacheKey, tokenJson, cacheOptions, cancellationToken);

            _logger.LogInformation("Generated download token for document {DocumentId}, version {VersionId}, user {UserId}",
                document.Id, versionId, UserId);

            return Ok(new DownloadTokenResponse
            {
                Token = token,
                ExpiresInSeconds = ttlSeconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating download token for version {VersionId}", versionId);
            return StatusCode(500, new { code = "TOKEN_ERROR", message = ex.Message });
        }
    }
}

/// <summary>
/// Metadata cho download token (lưu trong Redis)
/// </summary>
public class DownloadTokenMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
}
