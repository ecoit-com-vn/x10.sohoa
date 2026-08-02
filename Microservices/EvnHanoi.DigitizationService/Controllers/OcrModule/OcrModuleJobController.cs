using EvnHanoi.DigitizationService.Models.Dto;
using EvnHanoi.DigitizationService.Models.OcrModule;
using EvnHanoi.DigitizationService.Repositories.OcrModule;
using EvnHanoi.DigitizationService.Services.OcrModule;
using EvnHanoi.Infrastructure.Database;
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
    private readonly ILogger<OcrModuleJobController> _logger;

    public OcrModuleJobController(
        IOcrModuleRepository repository,
        IOcrJsonMaterializer materializer,
        ILogger<OcrModuleJobController> logger)
    {
        _repository = repository;
        _materializer = materializer;
        _logger = logger;
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
}
