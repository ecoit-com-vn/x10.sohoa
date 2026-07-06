using Minio;
using Minio.DataModel.Args;

namespace EvnHanoi.ReportService.Infrastructure.Services;

public interface IReportFileStorageService
{
    string DossierBucketName { get; }
    Task<Stream> DownloadFileAsync(string filePath, string? bucketName = null, CancellationToken cancellationToken = default);
}

public class ReportFileStorageService : IReportFileStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReportFileStorageService> _logger;

    public ReportFileStorageService(
        IMinioClient minioClient,
        IConfiguration configuration,
        ILogger<ReportFileStorageService> logger)
    {
        _minioClient = minioClient;
        _configuration = configuration;
        _logger = logger;
    }

    public string DossierBucketName =>
        _configuration["MinIO:DocumentBucketName"] ?? "documents";

    public async Task<Stream> DownloadFileAsync(
        string filePath,
        string? bucketName = null,
        CancellationToken cancellationToken = default)
    {
        var bucket = bucketName ?? DossierBucketName;
        var memoryStream = new MemoryStream();

        await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(filePath)
            .WithCallbackStream(stream =>
            {
                stream.CopyTo(memoryStream);
            }), cancellationToken);

        memoryStream.Position = 0;
        _logger.LogInformation("Downloaded file from MinIO bucket {Bucket}: {FilePath}", bucket, filePath);
        return memoryStream;
    }
}
