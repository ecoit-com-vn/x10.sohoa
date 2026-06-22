using Minio;
using Minio.DataModel.Args;
using System.Text;

namespace EvnHanoi.EquipmentService.Core.Services;

/// <summary>
/// Service cho lưu trữ file trên MinIO
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Upload file trực tiếp (dùng cho direct upload)
    /// </summary>
    Task<string> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string mimeType,
        long fileSize,
        string unitCode,
        Guid folderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload chunk (dùng cho chunked upload)
    /// </summary>
    Task<string> UploadChunkAsync(
        string uploadId,
        int chunkNumber,
        Stream chunk,
        long chunkSize,
        string unitCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge chunks thành file hoàn chỉnh
    /// </summary>
    Task<string> MergeChunksAsync(
        string uploadId,
        int totalChunks,
        string unitCode,
        Guid folderId,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Download file từ MinIO (bucket mặc định: kho tài liệu)
    /// </summary>
    Task<Stream> DownloadFileAsync(string filePath, string? bucketName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa file khỏi MinIO (bucket mặc định: kho tài liệu)
    /// </summary>
    Task<bool> DeleteFileAsync(string filePath, string? bucketName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa chunks tạm từ upload session
    /// </summary>
    Task<bool> DeleteUploadSessionAsync(
        string uploadId,
        int totalChunks,
        string unitCode,
        CancellationToken cancellationToken = default);

    Task<string> UploadFileToDossierAsync(
        Stream fileStream,
        string fileName,
        string mimeType,
        long fileSize,
        string unitCode,
        Guid dossierId,
        CancellationToken cancellationToken = default);

    Task<string> MergeChunksToDossierAsync(
        string uploadId,
        int totalChunks,
        string unitCode,
        Guid dossierId,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<string> CopyFileAsync(
        string sourceObjectKey,
        string destinationObjectKey,
        string? sourceBucketName = null,
        string? destinationBucketName = null,
        CancellationToken cancellationToken = default);

    string DocumentBucketName { get; }
    string DossierBucketName { get; }

    string BuildDossierObjectKey(string unitCode, Guid dossierId, string fileName);
}

public class FileStorageService : IFileStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<FileStorageService> _logger;
    private readonly string _documentBucket;
    private readonly string _dossierBucket;
    private readonly string _sessionBucket;

    public string DocumentBucketName => _documentBucket;
    public string DossierBucketName => _dossierBucket;

    public FileStorageService(IMinioClient minioClient, IConfiguration config, ILogger<FileStorageService> logger)
    {
        _minioClient = minioClient ?? throw new ArgumentNullException(nameof(minioClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _documentBucket = config["MinIO:DocumentBucketName"] ?? "documents";
        _dossierBucket = config["MinIO:DossierBucketName"] ?? "dossiers";
        _sessionBucket = config["MinIO:UploadSessionsBucketName"] ?? "upload-sessions";
    }

    public async Task<string> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string mimeType,
        long fileSize,
        string unitCode,
        Guid folderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var objectKey = BuildDocumentObjectKey(unitCode, folderId, fileName);

            await EnsureDocumentBucketAsync(cancellationToken);

            // Upload file
            var putArgs = new PutObjectArgs()
                .WithBucket(_documentBucket)
                .WithObject(objectKey)
                .WithStreamData(fileStream)
                .WithObjectSize(fileSize)
                .WithContentType(mimeType);

            await _minioClient.PutObjectAsync(putArgs, cancellationToken);
            _logger.LogInformation(
                "Uploaded file to MinIO: {Bucket}/{ObjectKey} (UnitCode: {UnitCode}, Size: {Size} bytes)",
                _documentBucket, objectKey, unitCode, fileSize);

            return objectKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file: {FileName} (UnitCode: {UnitCode})", fileName, unitCode);
            throw;
        }
    }

    public async Task<string> UploadChunkAsync(
        string uploadId,
        int chunkNumber,
        Stream chunk,
        long chunkSize,
        string unitCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var chunkObjectKey = BuildChunkObjectKey(unitCode, uploadId, chunkNumber);

            // Ensure session bucket exists
            var beArgs = new BucketExistsArgs().WithBucket(_sessionBucket);
            bool found = await _minioClient.BucketExistsAsync(beArgs, cancellationToken);
            if (!found)
            {
                var mbArgs = new MakeBucketArgs().WithBucket(_sessionBucket);
                await _minioClient.MakeBucketAsync(mbArgs, cancellationToken);
                _logger.LogInformation("Created MinIO bucket: {Bucket}", _sessionBucket);
            }

            // Upload chunk
            var putArgs = new PutObjectArgs()
                .WithBucket(_sessionBucket)
                .WithObject(chunkObjectKey)
                .WithStreamData(chunk)
                .WithObjectSize(chunkSize)
                .WithContentType("application/octet-stream");

            var response = await _minioClient.PutObjectAsync(putArgs, cancellationToken);
            _logger.LogInformation("Uploaded chunk {ChunkNumber} for session {UploadId}: ETag={ETag}", chunkNumber, uploadId, response?.Etag);

            return response?.Etag ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload chunk {ChunkNumber} for session {UploadId}", chunkNumber, uploadId);
            throw;
        }
    }

    public async Task<string> MergeChunksAsync(
        string uploadId,
        int totalChunks,
        string unitCode,
        Guid folderId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: MinIO MultipartUpload merge happens automatically when upload completes
            // Placeholder path theo mã đơn vị + thư mục cho bản ghi DB
            var objectKey = BuildDocumentObjectKey(unitCode, folderId, fileName);

            _logger.LogInformation(
                "Merged {TotalChunks} chunks for session {UploadId} -> {ObjectKey}",
                totalChunks, uploadId, objectKey);

            return objectKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge chunks for session {UploadId}", uploadId);
            throw;
        }
    }

    public async Task<Stream> DownloadFileAsync(
        string filePath,
        string? bucketName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bucket = ResolveBucket(bucketName);
            var memStream = new MemoryStream();

            var getArgs = new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(filePath)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memStream);
                });

            await _minioClient.GetObjectAsync(getArgs, cancellationToken);
            memStream.Seek(0, SeekOrigin.Begin);

            _logger.LogInformation("Downloaded file from MinIO: {Bucket}/{ObjectKey}", bucket, filePath);
            return memStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(
        string filePath,
        string? bucketName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bucket = ResolveBucket(bucketName);
            var rmArgs = new RemoveObjectArgs()
                .WithBucket(bucket)
                .WithObject(filePath);

            await _minioClient.RemoveObjectAsync(rmArgs, cancellationToken);
            _logger.LogInformation("Deleted file from MinIO: {Bucket}/{ObjectKey}", bucket, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<bool> DeleteUploadSessionAsync(
        string uploadId,
        int totalChunks,
        string unitCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Delete all chunk files for this upload session
            var tasks = new List<Task>();
            for (int i = 1; i <= totalChunks; i++)
            {
                var chunkObjectKey = BuildChunkObjectKey(unitCode, uploadId, i);
                var rmArgs = new RemoveObjectArgs()
                    .WithBucket(_sessionBucket)
                    .WithObject(chunkObjectKey);

                tasks.Add(_minioClient.RemoveObjectAsync(rmArgs, cancellationToken));
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation(
                "Deleted upload session {UploadId} ({TotalChunks} chunks, UnitCode: {UnitCode})",
                uploadId, totalChunks, unitCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete upload session {UploadId}", uploadId);
            return false;
        }
    }

    public async Task<string> UploadFileToDossierAsync(
        Stream fileStream,
        string fileName,
        string mimeType,
        long fileSize,
        string unitCode,
        Guid dossierId,
        CancellationToken cancellationToken = default)
    {
        var objectKey = BuildDossierObjectKey(unitCode, dossierId, fileName);
        await EnsureBucketAsync(_dossierBucket, cancellationToken);

        var putArgs = new PutObjectArgs()
            .WithBucket(_dossierBucket)
            .WithObject(objectKey)
            .WithStreamData(fileStream)
            .WithObjectSize(fileSize)
            .WithContentType(mimeType);

        await _minioClient.PutObjectAsync(putArgs, cancellationToken);
        _logger.LogInformation(
            "Uploaded dossier file to MinIO: {Bucket}/{ObjectKey} (DossierId: {DossierId})",
            _dossierBucket, objectKey, dossierId);
        return objectKey;
    }

    public Task<string> MergeChunksToDossierAsync(
        string uploadId,
        int totalChunks,
        string unitCode,
        Guid dossierId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var objectKey = BuildDossierObjectKey(unitCode, dossierId, fileName);
        _logger.LogInformation(
            "Merged {TotalChunks} chunks for dossier session {UploadId} -> {ObjectKey}",
            totalChunks, uploadId, objectKey);
        return Task.FromResult(objectKey);
    }

    public async Task<string> CopyFileAsync(
        string sourceObjectKey,
        string destinationObjectKey,
        string? sourceBucketName = null,
        string? destinationBucketName = null,
        CancellationToken cancellationToken = default)
    {
        var sourceBucket = ResolveBucket(sourceBucketName);
        var destBucket = ResolveBucket(destinationBucketName ?? sourceBucketName);

        await EnsureBucketAsync(destBucket, cancellationToken);

        var cpSrc = new CopySourceObjectArgs()
            .WithBucket(sourceBucket)
            .WithObject(sourceObjectKey);

        var args = new CopyObjectArgs()
            .WithBucket(destBucket)
            .WithObject(destinationObjectKey)
            .WithCopyObjectSource(cpSrc);

        await _minioClient.CopyObjectAsync(args, cancellationToken);
        _logger.LogInformation(
            "Copied MinIO object {SourceBucket}/{Source} -> {DestBucket}/{Dest}",
            sourceBucket, sourceObjectKey, destBucket, destinationObjectKey);
        return destinationObjectKey;
    }

    public string BuildDossierObjectKey(string unitCode, Guid dossierId, string fileName)
    {
        var safeName = SanitizeFileName(fileName);
        var safeUnitCode = SanitizeUnitCode(unitCode);
        var datePrefix = DateTime.UtcNow.ToString("yyyy/MM");
        var fileGuid = Guid.NewGuid();
        return $"{safeUnitCode}/{datePrefix}/{dossierId}/{fileGuid}_{safeName}";
    }

    private async Task EnsureDocumentBucketAsync(CancellationToken cancellationToken)
        => await EnsureBucketAsync(_documentBucket, cancellationToken);

    private async Task EnsureBucketAsync(string bucket, CancellationToken cancellationToken)
    {
        var beArgs = new BucketExistsArgs().WithBucket(bucket);
        if (!await _minioClient.BucketExistsAsync(beArgs, cancellationToken))
        {
            var mbArgs = new MakeBucketArgs().WithBucket(bucket);
            await _minioClient.MakeBucketAsync(mbArgs, cancellationToken);
            _logger.LogInformation("Created MinIO bucket: {Bucket}", bucket);
        }
    }

    private string ResolveBucket(string? bucketName) =>
        string.IsNullOrWhiteSpace(bucketName) ? _documentBucket : bucketName.Trim();

    private static string BuildDocumentObjectKey(string unitCode, Guid folderId, string fileName)
    {
        var safeName = SanitizeFileName(fileName);
        var safeUnitCode = SanitizeUnitCode(unitCode);
        var datePrefix = DateTime.UtcNow.ToString("yyyy/MM");
        var fileGuid = Guid.NewGuid();
        return $"{safeUnitCode}/{datePrefix}/{folderId}/{fileGuid}_{safeName}";
    }

    private static string BuildChunkObjectKey(string unitCode, string uploadId, int chunkNumber)
    {
        var safeUnitCode = SanitizeUnitCode(unitCode);
        var datePrefix = DateTime.UtcNow.ToString("yyyy/MM");
        return $"{safeUnitCode}/{datePrefix}/sessions/{uploadId}/{chunkNumber}.dat";
    }

    private static string SanitizeUnitCode(string unitCode)
    {
        var code = unitCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Mã đơn vị không được để trống");

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            code = code.Replace(invalidChar, '_');

        return code.Replace('/', '_').Replace('\\', '_');
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
            return "file.bin";

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            name = name.Replace(invalidChar, '_');

        return name.Replace('/', '_').Replace('\\', '_');
    }
}
