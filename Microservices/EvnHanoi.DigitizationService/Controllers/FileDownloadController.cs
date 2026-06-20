using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using EvnHanoi.DigitizationService.Services;
using System.Text.Json;

namespace EvnHanoi.DigitizationService.Controllers
{
    /// <summary>
    /// API stream file với one-time token (DigitizationService)
    /// </summary>
    [ApiController]
    [Route("api/v1/files")]
    public class FileDownloadController : ControllerBase
    {
        private readonly IMinioStorageService _minioStorageService;
        private readonly IDistributedCache _cache;
        private readonly ILogger<FileDownloadController> _logger;

        public FileDownloadController(
            IMinioStorageService minioStorageService,
            IDistributedCache cache,
            ILogger<FileDownloadController> logger)
        {
            _minioStorageService = minioStorageService ?? throw new ArgumentNullException(nameof(minioStorageService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Stream file bằng one-time download token
        /// </summary>
        [HttpGet("download")]
        public async Task<IActionResult> DownloadFileByToken(
            [FromQuery] string token,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                    return BadRequest("Token không được để trống");

                // Get token metadata from Redis
                var cacheKey = $"download:token:{token}";
                var tokenData = await _cache.GetStringAsync(cacheKey, cancellationToken);

                if (string.IsNullOrEmpty(tokenData))
                {
                    _logger.LogWarning("Download token not found or expired: {Token}", token);
                    return Forbid("Token không hợp lệ hoặc đã hết hạn");
                }

                // Parse token data
                var tokenMetadata = JsonSerializer.Deserialize<DownloadTokenMetadata>(tokenData);
                if (tokenMetadata == null)
                {
                    _logger.LogWarning("Invalid token metadata: {Token}", token);
                    return Forbid("Token không hợp lệ");
                }

                // Delete token immediately (one-time use)
                await _cache.RemoveAsync(cacheKey, cancellationToken);

                // Get file from MinIO
                _logger.LogInformation("Streaming file: {FilePath}", tokenMetadata.FilePath);
                var fileStream = await _minioStorageService.DownloadFileAsync(tokenMetadata.BucketName, tokenMetadata.FilePath);

                // Return file to client
                return File(fileStream, tokenMetadata.MimeType, tokenMetadata.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file with token");
                return StatusCode(500, new { code = "DOWNLOAD_ERROR", message = ex.Message });
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
}
