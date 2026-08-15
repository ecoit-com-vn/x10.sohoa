using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Models.Dto;
using EvnHanoi.DigitizationService.Models.OcrModule;
using EvnHanoi.DigitizationService.Repositories.OcrModule;
using EvnHanoi.DigitizationService.Services;
using EvnHanoi.DigitizationService.Services.OcrModule;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.DigitizationService.Controllers.OcrModule;

/// <summary>
/// Phân hệ Module OCR (Nhóm A) — quản lý Job trung tâm. Job có thể khởi tạo từ file mới upload
/// hoặc từ 1 tài liệu đã số hóa sẵn (đọc lại kết quả OCR đã có trên MinIO, không xử lý lại).
/// </summary>
[ApiController]
[Route("api/v1/ocr-module/jobs")]
public class OcrModuleJobController : ControllerBase
{
    private readonly IOcrModuleRepository _repository;
    private readonly IOcrJsonMaterializer _materializer;
    private readonly IMinioStorageService _minioStorageService;
    private readonly IMessagePublisher _messagePublisher;
    private readonly EvnHanoi.DocumentProcessing.IDocumentCompressionService _documentCompressionService;
    private readonly ILogger<OcrModuleJobController> _logger;
    private readonly string _trainingBucketName;

    private static readonly string[] AllowedUploadContentTypes = { "application/pdf" };

    public OcrModuleJobController(
        IOcrModuleRepository repository,
        IOcrJsonMaterializer materializer,
        IMinioStorageService minioStorageService,
        IMessagePublisher messagePublisher,
        EvnHanoi.DocumentProcessing.IDocumentCompressionService documentCompressionService,
        IConfiguration configuration,
        ILogger<OcrModuleJobController> logger)
    {
        _repository = repository;
        _materializer = materializer;
        _minioStorageService = minioStorageService;
        _messagePublisher = messagePublisher;
        _documentCompressionService = documentCompressionService;
        _logger = logger;
        // Bucket riêng cho dữ liệu huấn luyện AI-OCR (đã dùng sẵn bởi OcrTrainingDataController) —
        // tách biệt hoàn toàn khỏi bucket "digitization" chứa file hồ sơ/thiết bị thật.
        _trainingBucketName = configuration["MinIO:TrainingBucketName"] ?? "ocr-training";
    }

    /// <summary>
    /// Màn hình "Quản lý dữ liệu huấn luyện AI-OCR" — tải lên 1 file PDF độc lập (không gắn Dossier/
    /// Equipment) và đưa vào xử lý OCR ngay. Job tạo với SourceType=NewUpload, trạng thái Materializing
    /// cho tới khi OcrWorker OCR xong và nạp region (xem OcrWorker.PublishExtractionAndAckAsync).
    /// </summary>
    [HttpPost("from-upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<CreateJobResponse>> CreateFromUpload([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { code = "ERR_OCR_MODULE_NO_FILE", message = "Vui lòng chọn file PDF để tải lên." });

        if (file.Length > 50_000_000)
            return BadRequest(new { code = "ERR_OCR_MODULE_FILE_TOO_LARGE", message = "Kích thước file không được vượt quá 50MB." });

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) ||
            Array.IndexOf(AllowedUploadContentTypes, file.ContentType?.ToLowerInvariant()) < 0)
        {
            return BadRequest(new { code = "ERR_OCR_MODULE_INVALID_FILE_TYPE", message = "Chỉ chấp nhận file PDF." });
        }

        OcrModuleJob? job = null;

        try
        {
            using var inputStream = file.OpenReadStream();
            var compression = await _documentCompressionService.CompressAsync(inputStream, file.FileName, file.ContentType);
            using var compressedStream = compression.Stream;

            using var msPdf = new MemoryStream();
            await compressedStream.CopyToAsync(msPdf);
            var pdfBytes = msPdf.ToArray();
            var totalPages = PDFtoImage.Conversion.GetPageCount(pdfBytes);

            var objectName = $"training-upload/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}_{FileNameHelper.ToMinioObjectFileName(compression.FileName)}";
            using var uploadStream = new MemoryStream(pdfBytes);
            var filePath = await _minioStorageService.UploadFileAsync(_trainingBucketName, objectName, uploadStream, compression.MimeType);

            job = new OcrModuleJob
            {
                Id = UuidHelper.NewUuid(),
                SourceType = "NewUpload",
                SourceBucket = _trainingBucketName,
                SourceFilePath = filePath,
                TotalPages = totalPages,
                State = "Materializing",
                CreatedBy = User?.Identity?.Name ?? "System",
            };
            await _repository.CreateJobAsync(job);

            var taskMessage = new OcrTaskMessage
            {
                FileId = Guid.NewGuid(),
                FilePath = filePath,
                BucketName = _trainingBucketName,
                OcrModuleJobId = job.Id,
            };
            await _messagePublisher.PublishMessageAsync(taskMessage, "digitization.topic", "ocr.process.task");

            return Ok(new CreateJobResponse { JobId = job.Id, RegionCount = 0, State = job.State });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải lên file huấn luyện AI-OCR {FileName}", file.FileName);

            if (job != null)
            {
                try { await _repository.UpdateJobStateAsync(job.Id, "Failed", job.TotalPages, ex.Message); }
                catch { /* best-effort — không che lỗi gốc */ }
            }

            return StatusCode(500, new { code = "ERR_OCR_MODULE_UPLOAD_FAILED", message = "Không tải lên được file huấn luyện.", details = new[] { ex.Message } });
        }
    }

    /// <summary>Danh sách file đã tải lên cho màn hình "Quản lý dữ liệu huấn luyện AI-OCR".</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<OcrModuleJobListItemDto>>> GetUploadedJobs(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var result = await _repository.GetUploadedJobsPagedAsync(page, pageSize);
        return Ok(result);
    }

    /// <summary>Xóa mềm 1 file dữ liệu huấn luyện AI-OCR khỏi danh sách (không xóa vật lý trên MinIO).</summary>
    [HttpDelete("{jobId}")]
    public async Task<IActionResult> DeleteJob(string jobId)
    {
        var deleted = await _repository.SoftDeleteJobAsync(jobId);
        if (!deleted)
            return NotFound(new { code = "ERR_OCR_MODULE_JOB_NOT_FOUND", message = "Không tìm thấy Job." });

        return Ok(new { message = "Đã xóa dữ liệu huấn luyện." });
    }

    /// <summary>
    /// Tạo Job từ 1 tài liệu hồ sơ/thiết bị đã số hóa sẵn (dossier-management / equipment-documents
    /// truyền thẳng bucket + filePath đã biết) — materialize kết quả OCR đã có, không gọi lại ocr_vl_server.
    /// </summary>
    [HttpPost("from-existing")]
    public async Task<ActionResult<CreateJobResponse>> CreateFromExisting([FromBody] CreateJobFromExistingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Bucket) || string.IsNullOrWhiteSpace(request.FilePath))
            return BadRequest(new { code = "ERR_OCR_MODULE_INVALID_SOURCE", message = "Thiếu thông tin bucket/filePath nguồn dữ liệu." });

        var job = new OcrModuleJob
        {
            Id = UuidHelper.NewUuid(),
            SourceType = "ExistingDocument",
            SourceBucket = request.Bucket,
            SourceFilePath = request.FilePath,
            SourceDocumentVersionId = request.DocumentVersionId,
            TotalPages = request.TotalPages,
            State = "Materializing",
            CreatedBy = User?.Identity?.Name ?? "System",
        };

        await _repository.CreateJobAsync(job);

        try
        {
            var regions = await _materializer.MaterializeAsync(job.Id, job.SourceBucket, job.SourceFilePath, job.TotalPages);
            await _repository.InsertRegionsAsync(regions);

            var actualPages = regions.Count > 0 ? regions.Max(r => r.PageNumber) : job.TotalPages;
            await _repository.UpdateJobStateAsync(job.Id, "Ready", actualPages, null);

            return Ok(new CreateJobResponse { JobId = job.Id, RegionCount = regions.Count, State = "Ready" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi materialize OCR JSON cho Job {JobId}", job.Id);
            await _repository.UpdateJobStateAsync(job.Id, "Failed", job.TotalPages, ex.Message);
            return StatusCode(500, new { code = "ERR_OCR_MODULE_MATERIALIZE_FAILED", message = "Không đọc được kết quả OCR đã có của tài liệu này.", details = new[] { ex.Message } });
        }
    }

    [HttpGet("{jobId}")]
    public async Task<ActionResult<OcrModuleJob>> GetJob(string jobId)
    {
        var job = await _repository.GetJobByIdAsync(jobId);
        if (job == null)
            return NotFound(new { code = "ERR_OCR_MODULE_JOB_NOT_FOUND", message = "Không tìm thấy Job." });

        return Ok(job);
    }

    [HttpGet("{jobId}/regions")]
    public async Task<ActionResult<PagedResult<OcrModuleRegionDto>>> GetRegions(string jobId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var result = await _repository.GetRegionsPagedAsync(jobId, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Render 1 trang của file nguồn (PDF) ra ảnh JPEG ở OcrModuleImageDpi.DisplayDpi — KHÁC DPI mà
    /// OcrWorker dùng lúc OCR (OcrModuleImageDpi.OcrSourceDpi). Toạ độ box của mọi region trong
    /// OCR_MODULE_REGION đã được quy đổi về DisplayDpi trước khi lưu (xem OcrJsonMaterializer cho
    /// region Text, OcrModuleSealSignatureService cho region Seal/Signature — cả hai đều nhất quán
    /// với ảnh trả về ở đây), nên FE có thể dùng thẳng BoxX0/Y0/X1/Y1 mà không cần quy đổi thêm.
    /// </summary>
    [HttpGet("{jobId}/pages/{pageNumber}/image")]
    public async Task<IActionResult> GetPageImage(string jobId, int pageNumber)
    {
        var job = await _repository.GetJobByIdAsync(jobId);
        if (job == null)
            return NotFound(new { code = "ERR_OCR_MODULE_JOB_NOT_FOUND", message = "Không tìm thấy Job." });

        if (pageNumber < 1 || pageNumber > job.TotalPages)
            return BadRequest(new { code = "ERR_OCR_MODULE_INVALID_PAGE", message = "Số trang không hợp lệ." });

        try
        {
            using var fileStream = await _minioStorageService.DownloadFileAsync(job.SourceBucket, job.SourceFilePath);
            using var msPdf = new MemoryStream();
            await fileStream.CopyToAsync(msPdf);
            byte[] pdfBytes = msPdf.ToArray();

            using var imgStream = new MemoryStream();
            var renderOptions = new PDFtoImage.RenderOptions { Dpi = OcrModuleImageDpi.DisplayDpi, WithAnnotations = true };
            PDFtoImage.Conversion.SaveJpeg(imgStream, pdfBytes, password: null, page: pageNumber - 1, options: renderOptions);

            return File(imgStream.ToArray(), "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi render ảnh trang {PageNumber} cho Job {JobId}", pageNumber, jobId);
            return StatusCode(500, new { code = "ERR_OCR_MODULE_PAGE_IMAGE_FAILED", message = "Không render được ảnh trang tài liệu.", details = new[] { ex.Message } });
        }
    }
}
