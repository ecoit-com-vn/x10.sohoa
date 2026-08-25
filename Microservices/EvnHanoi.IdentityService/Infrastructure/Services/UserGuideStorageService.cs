using EvnHanoi.IdentityService.Core.Interfaces;
using EvnHanoi.Infrastructure.Utils;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace EvnHanoi.IdentityService.Infrastructure.Services;

public class UserGuideStorageService : IUserGuideStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
    };

    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB

    private readonly IMinioClient _minioClient;
    private readonly ILogger<UserGuideStorageService> _logger;

    public string BucketName { get; }

    public UserGuideStorageService(IConfiguration configuration, ILogger<UserGuideStorageService> logger)
    {
        _logger = logger;
        BucketName = configuration["MinIO:UserGuideBucketName"] ?? "user-guides";

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

    public async Task<string> UploadGuideFileAsync(string roleName, IFormFile file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Loại tệp không được hỗ trợ. Chỉ chấp nhận PDF, Word, Excel hoặc PowerPoint.");

        if (file.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("Kích thước tệp vượt quá giới hạn 50MB.");

        await EnsureBucketAsync(cancellationToken);

        var safeRole = FileNameHelper.ToMinioObjectFileName(string.IsNullOrWhiteSpace(roleName) ? "role" : roleName);
        var safeFileName = FileNameHelper.ToMinioObjectFileName(file.FileName);
        var objectKey = $"{safeRole}/{DateTime.UtcNow:yyyyMMddHHmmssfff}_{safeFileName}";

        await using var stream = file.OpenReadStream();
        var putArgs = new PutObjectArgs()
            .WithBucket(BucketName)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(file.Length)
            .WithContentType(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);

        await _minioClient.PutObjectAsync(putArgs, cancellationToken);
        return objectKey;
    }

    public async Task<(Stream Stream, string ContentType)> DownloadGuideFileAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var memoryStream = new MemoryStream();
        var statArgs = new StatObjectArgs()
            .WithBucket(BucketName)
            .WithObject(objectKey);
        var stat = await _minioClient.StatObjectAsync(statArgs, cancellationToken);

        var getArgs = new GetObjectArgs()
            .WithBucket(BucketName)
            .WithObject(objectKey)
            .WithCallbackStream(stream => stream.CopyTo(memoryStream));

        await _minioClient.GetObjectAsync(getArgs, cancellationToken);
        memoryStream.Position = 0;

        return (memoryStream, string.IsNullOrWhiteSpace(stat.ContentType) ? "application/octet-stream" : stat.ContentType);
    }

    public async Task DeleteGuideFileAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var removeArgs = new RemoveObjectArgs()
                .WithBucket(BucketName)
                .WithObject(objectKey);
            await _minioClient.RemoveObjectAsync(removeArgs, cancellationToken);
        }
        catch (ObjectNotFoundException)
        {
            _logger.LogWarning("User guide object not found while deleting: {Bucket}/{ObjectKey}", BucketName, objectKey);
        }
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var existsArgs = new BucketExistsArgs().WithBucket(BucketName);
        if (!await _minioClient.BucketExistsAsync(existsArgs, cancellationToken))
        {
            var makeArgs = new MakeBucketArgs().WithBucket(BucketName);
            await _minioClient.MakeBucketAsync(makeArgs, cancellationToken);
        }
    }
}
