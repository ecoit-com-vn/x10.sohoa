using System.Text.Json;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace EvnHanoi.EquipmentService.Core.Services;

public interface IFileDownloadTokenService
{
    Task<DownloadTokenResponse> CreateTokenAsync(
        string filePath,
        string fileName,
        string mimeType,
        string? bucketName = null,
        CancellationToken cancellationToken = default);
}

public class FileDownloadTokenService : IFileDownloadTokenService
{
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _config;
    private readonly ILogger<FileDownloadTokenService> _logger;

    public FileDownloadTokenService(
        IDistributedCache cache,
        IConfiguration config,
        ILogger<FileDownloadTokenService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DownloadTokenResponse> CreateTokenAsync(
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
        var metadata = new DownloadTokenMetadata
        {
            FilePath = filePath,
            BucketName = bucketName ?? _config["MinIO:DocumentBucketName"] ?? "documents",
            FileName = fileName,
            MimeType = mimeType ?? "application/octet-stream"
        };

        var cacheKey = $"download:token:{token}";
        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(metadata),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
            },
            cancellationToken);

        _logger.LogInformation("Generated download token for file {FileName}", fileName);

        return new DownloadTokenResponse
        {
            Token = token,
            ExpiresInSeconds = ttlSeconds
        };
    }
}

/// <summary>Metadata cho download token (lưu trong Redis).</summary>
public class DownloadTokenMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
}
