using EvnHanoi.Infrastructure.Security;
using EvnHanoi.NotificationService.Services;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.NotificationService.Controllers;

/// <summary>
/// Reindex đồng bộ một hồ sơ sau thay đổi workflow — gọi nội bộ từ EquipmentService.
/// </summary>
[ApiController]
[Route("internal/v1/dossiers")]
[BypassDynamicPermission]
public class InternalDossierIndexController : ControllerBase
{
    private readonly IDossierIndexer _indexer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalDossierIndexController> _logger;

    public InternalDossierIndexController(
        IDossierIndexer indexer,
        IConfiguration configuration,
        ILogger<InternalDossierIndexController> logger)
    {
        _indexer = indexer;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("{id}/reindex")]
    public async Task<IActionResult> Reindex(
        string id,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        CancellationToken cancellationToken)
    {
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
        {
            _logger.LogError("Internal:Token chưa cấu hình trên NotificationService — từ chối reindex nội bộ.");
            return StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên NotificationService." });
        }

        if (!string.Equals(internalToken, expected, StringComparison.Ordinal))
            return Unauthorized(new { message = "Token nội bộ không hợp lệ." });

        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "DossierId không hợp lệ." });

        var ok = await _indexer.IndexByIdAsync(id.Trim(), cancellationToken);
        if (!ok)
        {
            _logger.LogWarning("Internal reindex failed for dossier {DossierId}.", id);
            return StatusCode(500, new { message = "Không thể reindex hồ sơ." });
        }

        return Ok(new { success = true });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        string id,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        CancellationToken cancellationToken)
    {
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
        {
            _logger.LogError("Internal:Token chưa cấu hình trên NotificationService — từ chối xóa index nội bộ.");
            return StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên NotificationService." });
        }

        if (!string.Equals(internalToken, expected, StringComparison.Ordinal))
            return Unauthorized(new { message = "Token nội bộ không hợp lệ." });

        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "DossierId không hợp lệ." });

        var ok = await _indexer.DeleteByIdAsync(id.Trim(), cancellationToken);
        if (!ok)
        {
            _logger.LogWarning("Internal ES delete failed for dossier {DossierId}.", id);
            return StatusCode(500, new { message = "Không thể xóa hồ sơ khỏi Elasticsearch." });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Dọn document ES trùng _id legacy (chạy một lần sau migrate chuẩn hóa _id).
    /// </summary>
    [HttpPost("purge-legacy-document-ids")]
    public async Task<IActionResult> PurgeLegacyDocumentIds(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        CancellationToken cancellationToken)
    {
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
        {
            _logger.LogError("Internal:Token chưa cấu hình — từ chối purge legacy document ids.");
            return StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên NotificationService." });
        }

        if (!string.Equals(internalToken, expected, StringComparison.Ordinal))
            return Unauthorized(new { message = "Token nội bộ không hợp lệ." });

        var removed = await _indexer.PurgeLegacyDocumentIdsAsync(cancellationToken);
        return Ok(new { success = true, removed });
    }
}
