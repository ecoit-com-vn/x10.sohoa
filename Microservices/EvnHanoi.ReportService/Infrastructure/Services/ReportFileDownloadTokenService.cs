using System.Text.Json;
using EvnHanoi.ReportService.Core.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace EvnHanoi.ReportService.Infrastructure.Services;

public interface IReportFileDownloadTokenService
{
    Task<ReportDownloadTokenResponse> CreateTokenAsync(
        string filePath,
        string fileName,
        string mimeType,
        string? bucketName = null,
        CancellationToken cancellationToken = default);
}

public class ReportFileDownloadTokenService : IReportFileDownloadTokenService
{
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _config;
    private readonly ILogger<ReportFileDownloadTokenService> _logger;

    public ReportFileDownloadTokenService(
        IDistributedCache cache,
        IConfiguration config,
        ILogger<ReportFileDownloadTokenService> logger)
    {
        _cache = cache;
        _config = config;
        _logger = logger;
    }

    public async Task<ReportDownloadTokenResponse> CreateTokenAsync(
        string filePath,
        string fileName,
        string mimeType,
        string? bucketName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Tài liệu không có file");

        var token = Guid.NewGuid().ToString("N");
        var ttlSeconds = _config.GetValue<int>("FileDownload:OneTimeTokenTTLSeconds", 60);
        var metadata = new ReportDownloadTokenMetadata
        {
            FilePath = filePath,
            BucketName = bucketName ?? _config["MinIO:DocumentBucketName"] ?? "documents",
            FileName = fileName,
            MimeType = mimeType ?? "application/octet-stream"
        };

        await _cache.SetStringAsync(
            $"download:token:{token}",
            JsonSerializer.Serialize(metadata),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
            },
            cancellationToken);

        _logger.LogInformation("Generated report download token for file {FileName}", fileName);

        return new ReportDownloadTokenResponse
        {
            Token = token,
            ExpiresInSeconds = ttlSeconds
        };
    }
}

public class ReportDownloadTokenMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
}
