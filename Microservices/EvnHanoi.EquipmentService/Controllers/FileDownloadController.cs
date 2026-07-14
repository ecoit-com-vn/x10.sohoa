using System.Text.Json;
using EvnHanoi.EquipmentService.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// API stream file với one-time token (EquipmentService)
/// </summary>
[ApiController]
[Route("api/v1/files")]
public class FileDownloadController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<FileDownloadController> _logger;

    public FileDownloadController(
        IFileStorageService fileStorageService,
        IDistributedCache cache,
        ILogger<FileDownloadController> logger)
    {
        _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Stream file bằng one-time download token (không cần JWT)
    /// </summary>
    [HttpGet("download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadFileByToken(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { code = "VALIDATION_ERROR", message = "Token không được để trống" });

            var cacheKey = $"download:token:{token}";
            var tokenData = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (string.IsNullOrEmpty(tokenData))
            {
                _logger.LogWarning("Download token not found or expired: {Token}", token);
                return StatusCode(403, new { code = "FORBIDDEN", message = "Token không hợp lệ hoặc đã hết hạn" });
            }

            var tokenMetadata = JsonSerializer.Deserialize<DownloadTokenMetadata>(
                tokenData,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (tokenMetadata == null || string.IsNullOrWhiteSpace(tokenMetadata.FilePath))
            {
                _logger.LogWarning("Invalid token metadata: {Token}", token);
                return StatusCode(403, new { code = "FORBIDDEN", message = "Token không hợp lệ" });
            }

            await _cache.RemoveAsync(cacheKey, cancellationToken);

            _logger.LogInformation("Streaming file: {Bucket}/{FilePath} (Version: {VersionId})", tokenMetadata.BucketName, tokenMetadata.FilePath, tokenMetadata.VersionId);
            var fileStream = await _fileStorageService.DownloadFileAsync(
                tokenMetadata.FilePath,
                tokenMetadata.BucketName,
                tokenMetadata.VersionId,
                cancellationToken);

            return File(fileStream, tokenMetadata.MimeType, tokenMetadata.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file with token");
            return StatusCode(500, new { code = "DOWNLOAD_ERROR", message = ex.Message });
        }
    }
}
