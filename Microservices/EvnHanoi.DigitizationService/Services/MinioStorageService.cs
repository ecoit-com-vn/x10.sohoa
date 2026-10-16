using System;
using System.IO;
using System.Threading.Tasks;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.DigitizationService.Services
{
    public class MinioStorageService : IMinioStorageService
    {
        private readonly IMinioClient _minioClient;
        private readonly ILogger<MinioStorageService> _logger;

        public MinioStorageService(IConfiguration configuration, ILogger<MinioStorageService> logger)
        {
            _logger = logger;

            var endpoint = configuration["MinIO:Endpoint"] ?? "localhost:9000";
            var accessKey = configuration["MinIO:AccessKey"] ?? "minioadmin";
            var secretKey = configuration["MinIO:SecretKey"] ?? "minioadmin";
            var useSslConfig = configuration["MinIO:UseSSL"];
            bool useSsl = !string.IsNullOrEmpty(useSslConfig) && bool.Parse(useSslConfig);

            _minioClient = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(useSsl)
                .Build();
        }

        public async Task<string> UploadFileAsync(string bucketName, string objectName, Stream data, string contentType)
        {
            try
            {
                var beArgs = new BucketExistsArgs().WithBucket(bucketName);
                bool found = await _minioClient.BucketExistsAsync(beArgs);
                if (!found)
                {
                    var mbArgs = new MakeBucketArgs().WithBucket(bucketName);
                    await _minioClient.MakeBucketAsync(mbArgs);
                }

                var uploadStream = data;
                var disposeUploadStream = false;
                long objectSize;

                if (data.CanSeek)
                {
                    data.Position = 0;
                    objectSize = data.Length;
                }
                else
                {
                    var buffer = new MemoryStream();
                    await data.CopyToAsync(buffer);
                    buffer.Position = 0;
                    uploadStream = buffer;
                    disposeUploadStream = true;
                    objectSize = buffer.Length;
                }

                try
                {
                    var putObjectArgs = new PutObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(objectName)
                        .WithStreamData(uploadStream)
                        .WithObjectSize(objectSize)
                        .WithContentType(contentType);

                    await _minioClient.PutObjectAsync(putObjectArgs);
                }
                finally
                {
                    if (disposeUploadStream)
                        await uploadStream.DisposeAsync();
                }

                return objectName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file {ObjectName} to bucket {BucketName}", objectName, bucketName);
                throw;
            }
        }

        public async Task<Stream> DownloadFileAsync(string bucketName, string objectName)
        {
            var normalizedKey = NormalizeObjectKey(objectName);
            try
            {
                return await DownloadObjectCoreAsync(bucketName, normalizedKey);
            }
            catch (ObjectNotFoundException) when (!string.Equals(normalizedKey, objectName, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Object key chuẩn hóa không tồn tại, thử lại key gốc: {Bucket}/{ObjectName}",
                    bucketName,
                    objectName);
                return await DownloadObjectCoreAsync(bucketName, objectName);
            }
            catch (ObjectNotFoundException ex)
            {
                _logger.LogError(
                    ex,
                    "Không tìm thấy object MinIO {Bucket}/{ObjectName}. Kiểm tra file đã upload (chunk merge) hoặc object key trong DB.",
                    bucketName,
                    normalizedKey);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {ObjectName} from bucket {BucketName}", normalizedKey, bucketName);
                throw;
            }
        }

        private async Task<Stream> DownloadObjectCoreAsync(string bucketName, string objectName)
        {
            // Hỏi kích thước object trước rồi tải vào MemoryStream có mảng ĐÚNG cỡ (publiclyVisible → GetBuffer() trả đúng mảng đó). MemoryStream() rỗng tự nhân
            // đôi dung lượng khi lớn lên (file 186 MB tạm thời cần ~512 MB + bản cũ) — gây OutOfMemory với file lớn. Không lấy được kích thước thì quay về cách cũ.
            MemoryStream memoryStream;
            long objectSize = 0;
            try
            {
                var stat = await _minioClient.StatObjectAsync(new StatObjectArgs().WithBucket(bucketName).WithObject(objectName));
                objectSize = stat.Size;
            }
            catch (ObjectNotFoundException)
            {
                throw; // để DownloadFileAsync thử lại khoá gốc / báo lỗi như trước
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không lấy được kích thước object {Bucket}/{ObjectName} — tải bằng bộ đệm tự giãn.", bucketName, objectName);
            }

            if (objectSize > 0 && objectSize <= int.MaxValue - 64)
            {
                var buffer = new byte[objectSize];
                memoryStream = new MemoryStream(buffer, 0, buffer.Length, writable: true, publiclyVisible: true);
                memoryStream.SetLength(0); // bắt đầu ghi từ đầu; dung lượng cố định = kích thước object
            }
            else
            {
                memoryStream = new MemoryStream();
            }

            async Task DownloadIntoAsync(MemoryStream target)
            {
                var getObjectArgs = new GetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithCallbackStream(stream =>
                    {
                        stream.CopyTo(target);
                    });

                await _minioClient.GetObjectAsync(getObjectArgs);
            }

            try
            {
                await DownloadIntoAsync(memoryStream);
            }
            catch (NotSupportedException) when (objectSize > 0)
            {
                // Object đổi kích thước giữa lúc hỏi và lúc tải (ghi đè) → vượt dung lượng đã cấp: tải lại bằng bộ đệm tự giãn như cũ.
                memoryStream.Dispose();
                memoryStream = new MemoryStream();
                await DownloadIntoAsync(memoryStream);
            }
            memoryStream.Position = 0;
            return memoryStream;
        }

        private static string NormalizeObjectKey(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return objectName;

            if (objectName.Contains('%', StringComparison.Ordinal))
            {
                try
                {
                    return Uri.UnescapeDataString(objectName);
                }
                catch
                {
                    return objectName;
                }
            }

            return objectName;
        }
    }
}
