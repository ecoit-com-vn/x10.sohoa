using System.Text;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.NotificationService.Services;

public class MinioOcrTextReader : IMinioOcrTextReader
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioOcrTextReader> _logger;

    public MinioOcrTextReader(IConfiguration configuration, ILogger<MinioOcrTextReader> logger)
    {
        _logger = logger;

        var endpoint = configuration["MinIO:Endpoint"]
            ?? configuration["Minio:Endpoint"]
            ?? "localhost:9000";
        var accessKey = configuration["MinIO:AccessKey"]
            ?? configuration["Minio:AccessKey"]
            ?? "minioadmin";
        var secretKey = configuration["MinIO:SecretKey"]
            ?? configuration["Minio:SecretKey"]
            ?? "minioadmin";
        var useSslConfig = configuration["MinIO:UseSSL"]
            ?? configuration["Minio:UseSSL"]
            ?? configuration["Minio:Secure"];
        var useSsl = !string.IsNullOrEmpty(useSslConfig) && bool.Parse(useSslConfig);

        _minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();
    }

    public async Task<string> ReadConcatenatedMarkdownAsync(
        string bucketName,
        string pdfFilePath,
        int totalPagesHint,
        CancellationToken cancellationToken = default)
    {
        var baseFilePath = pdfFilePath;
        if (baseFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            baseFilePath = baseFilePath[..^4];

        var builder = new StringBuilder();
        var page = 1;
        var maxPages = totalPagesHint > 0 ? totalPagesHint : 500;

        while (page <= maxPages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mdFileName = $"{baseFilePath}_page_{page}.md";

            try
            {
                using var stream = await DownloadObjectAsync(bucketName, mdFileName, cancellationToken);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var pageText = await reader.ReadToEndAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    if (builder.Length > 0)
                        builder.AppendLine();
                    builder.AppendLine(pageText.Trim());
                }
                page++;
            }
            catch (ObjectNotFoundException)
            {
                if (totalPagesHint > 0 && page <= totalPagesHint)
                {
                    _logger.LogWarning(
                        "Thiếu file markdown trang {Page}/{TotalPages}: {Bucket}/{Object}",
                        page, totalPagesHint, bucketName, mdFileName);
                    page++;
                    continue;
                }
                break;
            }
        }

        return builder.ToString().Trim();
    }

    private async Task<Stream> DownloadObjectAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken)
    {
        var memoryStream = new MemoryStream();
        var getObjectArgs = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream => stream.CopyTo(memoryStream));

        await _minioClient.GetObjectAsync(getObjectArgs, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }
}
