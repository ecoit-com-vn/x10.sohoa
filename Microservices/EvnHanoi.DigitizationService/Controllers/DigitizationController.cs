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
        private readonly EvnHanoi.DocumentProcessing.IDocumentCompressionService _documentCompressionService;
        private readonly ILogger<DigitizationController> _logger;
        private readonly string _bucketName;

        public DigitizationController(
            IMinioStorageService minioStorageService,
            IMessagePublisher messagePublisher,
            IFileAttachmentRepository repository,
            EvnHanoi.DocumentProcessing.IDocumentCompressionService documentCompressionService,
            IConfiguration configuration,
            ILogger<DigitizationController> logger)
        {
            _minioStorageService = minioStorageService;
            _messagePublisher = messagePublisher;
            _repository = repository;
            _documentCompressionService = documentCompressionService;
            _logger = logger;
            _bucketName = configuration["MinIO:BucketName"] ?? "digitization";
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            try
            {
                // ===== NÉN FILE (giảm về ~150 DPI cho PDF scan/ảnh, giữ nguyên PDF điện tử gốc) =====
                using var stream = file.OpenReadStream();
                var compression = await _documentCompressionService.CompressAsync(stream, file.FileName, file.ContentType);
                using var compressedStream = compression.Stream;

                var objectName = $"{Guid.NewGuid()}_{FileNameHelper.ToMinioObjectFileName(compression.FileName)}";

                // 1. Upload to MinIO
                var filePath = await _minioStorageService.UploadFileAsync(_bucketName, objectName, compressedStream, compression.MimeType);

                // 2. Save to DB FILE_ATTACHMENT
                var fileAttachment = new FileAttachment
                {
                    FileName = compression.FileName,
                    FilePath = filePath,
                    ContentType = compression.MimeType,
                    FileSize = compression.Size,
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
