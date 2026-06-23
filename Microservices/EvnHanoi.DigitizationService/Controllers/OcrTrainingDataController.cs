using System;
using System.IO;
using System.Threading.Tasks;
using EvnHanoi.DigitizationService.Models;
using EvnHanoi.DigitizationService.Models.Dto;
using EvnHanoi.DigitizationService.Repositories;
using EvnHanoi.DigitizationService.Services;
using EvnHanoi.Infrastructure.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.DigitizationService.Controllers
{
    /// <summary>
    /// API Controller quản lý dữ liệu huấn luyện AI-OCR.
    /// Hỗ trợ upload ảnh/PDF, gán nhãn, xác nhận chất lượng, và xóa bản ghi.
    /// </summary>
    [ApiController]
    [Route("api/v1/ocr-training")]
    public class OcrTrainingDataController : ControllerBase
    {
        private readonly IOcrTrainingDataRepository _repository;
        private readonly IMinioStorageService _minioStorageService;
        private readonly ILogger<OcrTrainingDataController> _logger;
        private readonly string _bucketName;

        private static readonly string[] AllowedContentTypes = new[]
        {
            "image/jpeg", "image/png", "image/tiff", "image/bmp",
            "application/pdf"
        };

        public OcrTrainingDataController(
            IOcrTrainingDataRepository repository,
            IMinioStorageService minioStorageService,
            IConfiguration configuration,
            ILogger<OcrTrainingDataController> logger)
        {
            _repository = repository;
            _minioStorageService = minioStorageService;
            _logger = logger;
            _bucketName = configuration["Minio:TrainingBucketName"] ?? "ocr-training";
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/v1/ocr-training
        // Lấy danh sách dữ liệu huấn luyện có phân trang và lọc
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? documentType = null,
            [FromQuery] string? trainingStatus = null,
            [FromQuery] string? keyword = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var result = await _repository.GetPagedAsync(page, pageSize, documentType, trainingStatus, keyword);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/v1/ocr-training/{id}
        // Xem chi tiết một bản ghi
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            var data = await _repository.GetByIdAsync(id);
            if (data == null)
                return NotFound(new { Message = $"Không tìm thấy bản ghi huấn luyện với ID = {id}" });

            var dto = MapToDetail(data);
            return Ok(dto);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // POST /api/v1/ocr-training/upload
        // Upload file ảnh/PDF mới vào kho dữ liệu huấn luyện
        // ─────────────────────────────────────────────────────────────────────────
        [HttpPost("upload")]
        [RequestSizeLimit(50_000_000)] // Giới hạn 50MB
        public async Task<IActionResult> Upload([FromForm] UploadTrainingDataRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { Message = "Vui lòng chọn file để upload." });

            if (request.File.Length > 50_000_000)
                return BadRequest(new { Message = "Kích thước file không được vượt quá 50MB." });

            if (Array.IndexOf(AllowedContentTypes, request.File.ContentType.ToLower()) < 0)
                return BadRequest(new { Message = "Chỉ chấp nhận các định dạng: JPEG, PNG, TIFF, BMP, PDF." });

            try
            {
                var objectName = $"training/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}_{FileNameHelper.ToMinioObjectFileName(request.File.FileName)}";

                using var stream = request.File.OpenReadStream();
                var filePath = await _minioStorageService.UploadFileAsync(
                    _bucketName, objectName, stream, request.File.ContentType);

                var data = new OcrTrainingData
                {
                    FileName = request.File.FileName,
                    FilePath = filePath,
                    BucketName = _bucketName,
                    ContentType = request.File.ContentType,
                    FileSize = request.File.Length,
                    DocumentType = request.DocumentType,
                    LabelText = request.LabelText,
                    Notes = request.Notes,
                    TrainingStatus = string.IsNullOrWhiteSpace(request.LabelText) ? "Pending" : "Labeled",
                    UploadedBy = request.UploadedBy,
                    UploadedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var id = await _repository.CreateAsync(data);

                _logger.LogInformation("OCR Training file uploaded: {FileName}, ID={Id}, By={UploadedBy}",
                    request.File.FileName, id, request.UploadedBy);

                return Ok(new
                {
                    Id = id,
                    Message = "Upload dữ liệu huấn luyện thành công.",
                    TrainingStatus = data.TrainingStatus
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi upload file huấn luyện: {FileName}", request.File.FileName);
                return StatusCode(500, new { Message = "Lỗi hệ thống khi upload file. Vui lòng thử lại." });
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PUT /api/v1/ocr-training/{id}/label
        // Cập nhật nhãn văn bản và thông tin chú thích
        // ─────────────────────────────────────────────────────────────────────────
        [HttpPut("{id:long}/label")]
        public async Task<IActionResult> UpdateLabel(long id, [FromBody] UpdateTrainingDataRequest request)
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { Message = $"Không tìm thấy bản ghi với ID = {id}" });

            await _repository.UpdateLabelAsync(
                id,
                request.LabelText,
                request.DocumentType,
                request.TrainingStatus,
                request.QualityScore,
                request.Notes);

            _logger.LogInformation("OCR Training label updated for ID={Id}", id);
            return Ok(new { Message = "Cập nhật nhãn dữ liệu huấn luyện thành công." });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // POST /api/v1/ocr-training/{id}/verify
        // Chuyên gia xác nhận hoặc từ chối bản ghi
        // ─────────────────────────────────────────────────────────────────────────
        [HttpPost("{id:long}/verify")]
        public async Task<IActionResult> Verify(long id, [FromBody] VerifyTrainingDataRequest request)
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { Message = $"Không tìm thấy bản ghi với ID = {id}" });

            if (string.IsNullOrWhiteSpace(request.VerifiedBy))
                return BadRequest(new { Message = "Vui lòng cung cấp tên người xác nhận." });

            await _repository.VerifyAsync(id, request.IsVerified, request.VerifiedBy, request.Notes);

            var statusMsg = request.IsVerified ? "Đã xác nhận (Verified)" : "Đã từ chối (Rejected)";
            _logger.LogInformation("OCR Training ID={Id} {Status} by {Verifier}", id, statusMsg, request.VerifiedBy);

            return Ok(new { Message = $"Bản ghi #{id} {statusMsg}." });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // DELETE /api/v1/ocr-training/{id}
        // Xóa bản ghi dữ liệu huấn luyện
        // ─────────────────────────────────────────────────────────────────────────
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { Message = $"Không tìm thấy bản ghi với ID = {id}" });

            await _repository.DeleteAsync(id);
            _logger.LogInformation("OCR Training data deleted: ID={Id}", id);
            return Ok(new { Message = "Đã xóa bản ghi dữ liệu huấn luyện." });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/v1/ocr-training/statistics
        // Thống kê tổng quan dữ liệu huấn luyện (dùng cho dashboard)
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            var pending = await _repository.GetCountByStatusAsync("Pending");
            var labeled = await _repository.GetCountByStatusAsync("Labeled");
            var verified = await _repository.GetCountByStatusAsync("Verified");
            var rejected = await _repository.GetCountByStatusAsync("Rejected");

            return Ok(new
            {
                Pending = pending,
                Labeled = labeled,
                Verified = verified,
                Rejected = rejected,
                Total = pending + labeled + verified + rejected
            });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Private Helpers
        // ─────────────────────────────────────────────────────────────────────────
        private static OcrTrainingDataDetailDto MapToDetail(OcrTrainingData data)
        {
            return new OcrTrainingDataDetailDto
            {
                Id = data.Id,
                FileName = data.FileName,
                FilePath = data.FilePath,
                BucketName = data.BucketName,
                ContentType = data.ContentType,
                FileSize = data.FileSize,
                DocumentType = data.DocumentType,
                LabelText = data.LabelText,
                QualityScore = data.QualityScore,
                TrainingStatus = data.TrainingStatus,
                IsVerified = data.IsVerified,
                Notes = data.Notes,
                VerifiedBy = data.VerifiedBy,
                VerifiedAt = data.VerifiedAt,
                UploadedBy = data.UploadedBy,
                UploadedAt = data.UploadedAt,
                CreatedAt = data.CreatedAt,
                UpdatedAt = data.UpdatedAt
            };
        }
    }
}
