using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Security;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// API tạo download token cho file trong kho thư mục (EquipmentService)
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/files")]
public class FileDownloadTokenController : ControllerBase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileDownloadTokenService _downloadTokenService;
    private readonly ILogger<FileDownloadTokenController> _logger;

    public FileDownloadTokenController(
        IDocumentRepository documentRepository,
        IFileDownloadTokenService downloadTokenService,
        ILogger<FileDownloadTokenController> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _downloadTokenService = downloadTokenService ?? throw new ArgumentNullException(nameof(downloadTokenService));
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
    /// Tạo download token cho file version trong kho thư mục
    /// </summary>
    [HttpGet("{versionId:guid}/download-url")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDownloadToken(
        [FromRoute] Guid versionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var version = await _documentRepository.GetDocumentVersionByIdAsync(versionId);
            if (version == null)
                return NotFound("Phiên bản tài liệu không tồn tại");

            var document = await _documentRepository.GetDocumentByIdAsync(version.DocumentId);
            if (document == null)
                return NotFound("Tài liệu không tồn tại");

            if (document.DossierId.HasValue)
                return BadRequest(new { code = "INVALID_SCOPE", message = "Tài liệu thuộc hồ sơ — dùng API download của hồ sơ." });

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

            var result = await _downloadTokenService.CreateTokenAsync(
                version.FilePath,
                document.Name,
                version.MimeType ?? "application/octet-stream",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Generated folder download token for document {DocumentId}, version {VersionId}, user {UserId}",
                document.Id, versionId, UserId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating download token for version {VersionId}", versionId);
            return StatusCode(500, new { code = "TOKEN_ERROR", message = ex.Message });
        }
    }
}
