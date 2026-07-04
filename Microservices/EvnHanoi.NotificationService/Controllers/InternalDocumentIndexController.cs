using EvnHanoi.Infrastructure.Security;
using EvnHanoi.NotificationService.Repositories;
using EvnHanoi.NotificationService.Services;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.NotificationService.Controllers;

/// <summary>
/// Reindex/xóa đồng bộ document_index — gọi nội bộ hoặc script backfill.
/// </summary>
[ApiController]
[Route("internal/v1/documents")]
[BypassDynamicPermission]
public class InternalDocumentIndexController : ControllerBase
{
    private readonly IDocumentIndexer _indexer;
    private readonly IDocumentEnrichmentRepository _enrichmentRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalDocumentIndexController> _logger;

    public InternalDocumentIndexController(
        IDocumentIndexer indexer,
        IDocumentEnrichmentRepository enrichmentRepository,
        IConfiguration configuration,
        ILogger<InternalDocumentIndexController> logger)
    {
        _indexer = indexer;
        _enrichmentRepository = enrichmentRepository;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("{versionId}/reindex")]
    public async Task<IActionResult> Reindex(
        string versionId,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        CancellationToken cancellationToken)
    {
        if (!ValidateToken(internalToken, out var tokenError))
            return tokenError!;

        if (string.IsNullOrWhiteSpace(versionId))
            return BadRequest(new { message = "DocumentVersionId không hợp lệ." });

        var ok = await _indexer.IndexByVersionIdAsync(versionId.Trim(), null, null, 0, cancellationToken);
        if (!ok)
        {
            _logger.LogWarning("Internal reindex failed for document version {VersionId}.", versionId);
            return StatusCode(500, new { message = "Không thể reindex tài liệu." });
        }

        return Ok(new { success = true });
    }

    [HttpPost("reindex-all")]
    public async Task<IActionResult> ReindexAll(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        CancellationToken cancellationToken)
    {
        if (!ValidateToken(internalToken, out var tokenError))
            return tokenError!;

        var versionIds = (await _enrichmentRepository.GetIndexableVersionIdsAsync()).ToList();
        var indexed = 0;
        var failed = 0;

        foreach (var versionId in versionIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var ok = await _indexer.IndexByVersionIdAsync(versionId, null, null, 0, cancellationToken);
            if (ok)
                indexed++;
            else
                failed++;
        }

        _logger.LogInformation(
            "Backfill document_index completed: total={Total}, indexed={Indexed}, failed={Failed}.",
            versionIds.Count,
            indexed,
            failed);

        return Ok(new { success = true, total = versionIds.Count, indexed, failed });
    }

    /// <summary>
    /// Reindex tài liệu đã OCR thuộc hồ sơ Approved + Published (phạm vi hiển thị tìm kiếm toàn văn).
    /// </summary>
    [HttpPost("reindex-published")]
    public async Task<IActionResult> ReindexPublished(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        CancellationToken cancellationToken)
    {
        if (!ValidateToken(internalToken, out var tokenError))
            return tokenError!;

        var versionIds = (await _enrichmentRepository.GetPublishedIndexableVersionIdsAsync()).ToList();
        var indexed = 0;
        var failed = 0;

        foreach (var versionId in versionIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var ok = await _indexer.IndexByVersionIdAsync(versionId, null, null, 0, cancellationToken);
            if (ok)
                indexed++;
            else
                failed++;
        }

        _logger.LogInformation(
            "Reindex published dossier documents completed: total={Total}, indexed={Indexed}, failed={Failed}.",
            versionIds.Count,
            indexed,
            failed);

        return Ok(new { success = true, total = versionIds.Count, indexed, failed });
    }

    [HttpDelete("{versionId}")]
    public async Task<IActionResult> Delete(
        string versionId,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        CancellationToken cancellationToken)
    {
        if (!ValidateToken(internalToken, out var tokenError))
            return tokenError!;

        if (string.IsNullOrWhiteSpace(versionId))
            return BadRequest(new { message = "DocumentVersionId không hợp lệ." });

        var ok = await _indexer.DeleteByVersionIdAsync(versionId.Trim(), cancellationToken);
        if (!ok)
        {
            _logger.LogWarning("Internal ES delete failed for document version {VersionId}.", versionId);
            return StatusCode(500, new { message = "Không thể xóa tài liệu khỏi Elasticsearch." });
        }

        return Ok(new { success = true });
    }

    private bool ValidateToken(string? internalToken, out IActionResult? errorResult)
    {
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
        {
            _logger.LogError("Internal:Token chưa cấu hình trên NotificationService — từ chối thao tác nội bộ document index.");
            errorResult = StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên NotificationService." });
            return false;
        }

        if (!string.Equals(internalToken, expected, StringComparison.Ordinal))
        {
            errorResult = Unauthorized(new { message = "Token nội bộ không hợp lệ." });
            return false;
        }

        errorResult = null;
        return true;
    }
}
