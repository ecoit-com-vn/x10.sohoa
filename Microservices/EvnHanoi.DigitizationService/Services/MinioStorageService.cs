using System;
using System.IO;
using System.Threading.Tasks;
using Minio;
using Minio.DataModel.Args;
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
            
            var endpoint = configuration["Minio:Endpoint"];
            var accessKey = configuration["Minio:AccessKey"];
            var secretKey = configuration["Minio:SecretKey"];
            
            var useSslConfig = configuration["Minio:UseSSL"];
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
    }
}
