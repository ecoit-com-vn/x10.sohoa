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

            var endpoint = configuration["Minio:Endpoint"]
                ?? configuration["MinIO:Endpoint"]
                ?? "localhost:9000";
            var accessKey = configuration["Minio:AccessKey"]
                ?? configuration["MinIO:AccessKey"]
                ?? "minioadmin";
            var secretKey = configuration["Minio:SecretKey"]
                ?? configuration["MinIO:SecretKey"]
                ?? "minioadmin";

            var useSslConfig = configuration["Minio:UseSSL"]
                ?? configuration["Minio:Secure"]
                ?? configuration["MinIO:UseSSL"];
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

                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithStreamData(data)
                    .WithObjectSize(data.Length)
                    .WithContentType(contentType);

                await _minioClient.PutObjectAsync(putObjectArgs);

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
            var memoryStream = new MemoryStream();
            var getObjectArgs = new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                });

            await _minioClient.GetObjectAsync(getObjectArgs);
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
