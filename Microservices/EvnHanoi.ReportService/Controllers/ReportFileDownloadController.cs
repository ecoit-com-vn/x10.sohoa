using System.Text.Json;
using EvnHanoi.ReportService.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace EvnHanoi.ReportService.Controllers;

[ApiController]
[Route("api/v1/reports/files")]
public class ReportFileDownloadController : ControllerBase
{
    private readonly IReportFileStorageService _fileStorageService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ReportFileDownloadController> _logger;

    public ReportFileDownloadController(
        IReportFileStorageService fileStorageService,
        IDistributedCache cache,
        ILogger<ReportFileDownloadController> logger)
    {
        _fileStorageService = fileStorageService;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet("download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadFileByToken(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Token không được để trống" });

        var cacheKey = $"download:token:{token}";
        var tokenData = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (string.IsNullOrEmpty(tokenData))
        {
            _logger.LogWarning("Report download token not found or expired: {Token}", token);
            return StatusCode(403, new { code = "FORBIDDEN", message = "Token không hợp lệ hoặc đã hết hạn" });
        }

        var tokenMetadata = JsonSerializer.Deserialize<ReportDownloadTokenMetadata>(
            tokenData,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (tokenMetadata is null || string.IsNullOrWhiteSpace(tokenMetadata.FilePath))
            return StatusCode(403, new { code = "FORBIDDEN", message = "Token không hợp lệ" });

        await _cache.RemoveAsync(cacheKey, cancellationToken);

        var fileStream = await _fileStorageService.DownloadFileAsync(
            tokenMetadata.FilePath,
            tokenMetadata.BucketName,
            cancellationToken);

        return File(fileStream, tokenMetadata.MimeType, tokenMetadata.FileName);
    }
}
