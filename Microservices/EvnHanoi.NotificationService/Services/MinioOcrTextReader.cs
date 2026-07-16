using System.Text;
using System.Text.Json;
using System.Linq;
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

    private sealed class OcrTextBoxItem
    {
        public string? Text { get; set; }
    }

    private static readonly JsonSerializerOptions OcrJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MinioOcrTextReader(IConfiguration configuration, ILogger<MinioOcrTextReader> logger)
    {
        _logger = logger;

        var endpoint = configuration["MinIO:Endpoint"] ?? "localhost:9000";
        var accessKey = configuration["MinIO:AccessKey"] ?? "minioadmin";
        var secretKey = configuration["MinIO:SecretKey"] ?? "minioadmin";
        var useSslConfig = configuration["MinIO:UseSSL"];
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
            var jsonFileName = $"{baseFilePath}_page_{page}.json";
            var mdFileName = $"{baseFilePath}_page_{page}.md";

            string? pageText = null;
            try
            {
                using var stream = await DownloadObjectAsync(bucketName, jsonFileName, cancellationToken);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var jsonText = await reader.ReadToEndAsync(cancellationToken);

                // OCR json: mảng [{text, box, confidence}, ...]
                var items = JsonSerializer.Deserialize<List<OcrTextBoxItem>>(jsonText, OcrJsonOptions);
                pageText = items?
                    .Select(i => i.Text?.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Aggregate(string.Empty, (acc, t) =>
                        string.IsNullOrEmpty(acc) ? t : $"{acc} {t}");
                pageText = string.IsNullOrWhiteSpace(pageText) ? null : pageText.Trim();
            }
            catch (ObjectNotFoundException)
            {
                // Backward compatibility: có thể còn file .md ở dữ liệu OCR cũ.
                try
                {
                    using var stream = await DownloadObjectAsync(bucketName, mdFileName, cancellationToken);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var mdText = await reader.ReadToEndAsync(cancellationToken);
                    pageText = string.IsNullOrWhiteSpace(mdText) ? null : mdText.Trim();
                }
                catch (ObjectNotFoundException)
                {
                    page++;
                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(pageText))
            {
                if (builder.Length > 0)
                    builder.AppendLine();
                builder.AppendLine(pageText);
            }

            page++;
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
