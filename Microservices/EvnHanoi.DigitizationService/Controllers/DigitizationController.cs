using EvnHanoi.Infrastructure.Utils;
using System.Threading.Tasks;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Repositories;
using EvnHanoi.DigitizationService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.DigitizationService.Controllers
{
    [ApiController]
    [Route("api/v1/digitization")]
    public class DigitizationController : ControllerBase
    {
        private readonly IMinioStorageService _minioStorageService;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IFileAttachmentRepository _repository;
        private readonly ILogger<DigitizationController> _logger;
        private readonly string _bucketName;

        public DigitizationController(
            IMinioStorageService minioStorageService,
            IMessagePublisher messagePublisher,
            IFileAttachmentRepository repository,
            IConfiguration configuration,
            ILogger<DigitizationController> logger)
        {
            _minioStorageService = minioStorageService;
            _messagePublisher = messagePublisher;
            _repository = repository;
            _logger = logger;
            _bucketName = configuration["Minio:BucketName"] ?? "digitization";
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            try
            {
                var objectName = $"{Guid.NewGuid()}_{FileNameHelper.ToMinioObjectFileName(file.FileName)}";
                
                // 1. Upload to MinIO
                using var stream = file.OpenReadStream();
                var filePath = await _minioStorageService.UploadFileAsync(_bucketName, objectName, stream, file.ContentType);

                // 2. Save to DB FILE_ATTACHMENT
                var fileAttachment = new FileAttachment
                {
                    FileName = file.FileName,
                    FilePath = filePath,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    UploadedAt = DateTime.UtcNow,
                    UploadedBy = "System", // Ideally from User context
                    Status = "Uploaded"
                };

                var fileId = await _repository.CreateAsync(fileAttachment);

                // 3. Publish message to RabbitMQ
                var message = new OcrTaskMessage
                {
                    FileId = fileId,
                    FilePath = filePath,
                    BucketName = _bucketName
                };

                await _messagePublisher.PublishMessageAsync(
                    message: message,
                    exchange: "digitization.topic",
                    routingKey: "ocr.process.task"
                );

                return Ok(new { FileId = fileId, Message = "File uploaded and task queued successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while uploading file");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
// End of file
