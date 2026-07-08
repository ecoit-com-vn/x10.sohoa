using EvnHanoi.IdentityService.Core.Interfaces;
using EvnHanoi.Infrastructure.Utils;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace EvnHanoi.IdentityService.Infrastructure.Services;

public class AvatarStorageService : IAvatarStorageService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    };

    private readonly IMinioClient _minioClient;
    private readonly ILogger<AvatarStorageService> _logger;

    public string BucketName { get; }

    public AvatarStorageService(IConfiguration configuration, ILogger<AvatarStorageService> logger)
    {
        _logger = logger;
        BucketName = configuration["MinIO:AvatarBucketName"] ?? "user-avatars";

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

    public async Task<string> UploadAvatarAsync(string userId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new InvalidOperationException("Ảnh đại diện chỉ hỗ trợ JPG, PNG hoặc WEBP.");

        await EnsureBucketAsync(cancellationToken);

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ContentTypeToExtension(file.ContentType);

        var safeFileName = FileNameHelper.ToMinioObjectFileName(Path.GetFileNameWithoutExtension(file.FileName));
        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = "avatar";

        var objectKey = $"avatars/{userId}/{DateTime.UtcNow:yyyyMMddHHmmssfff}_{safeFileName}{extension.ToLowerInvariant()}";

        await using var stream = file.OpenReadStream();
        var putArgs = new PutObjectArgs()
            .WithBucket(BucketName)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(file.Length)
            .WithContentType(file.ContentType);

        await _minioClient.PutObjectAsync(putArgs, cancellationToken);
        return objectKey;
    }

    public async Task<(Stream Stream, string ContentType)> DownloadAvatarAsync(string objectKey, CancellationToken cancellationToken = default)
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

        return (memoryStream, string.IsNullOrWhiteSpace(stat.ContentType) ? GuessContentType(objectKey) : stat.ContentType);
    }

    public async Task DeleteAvatarAsync(string objectKey, CancellationToken cancellationToken = default)
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
            _logger.LogWarning("Avatar object not found while deleting: {Bucket}/{ObjectKey}", BucketName, objectKey);
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

    private static string ContentTypeToExtension(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg"
    };

    private static string GuessContentType(string objectKey) => Path.GetExtension(objectKey).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}
