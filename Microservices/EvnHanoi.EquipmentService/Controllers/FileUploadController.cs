using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Services;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// API upload file cho thư mục tài liệu
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/files")]
public class FileUploadController : ControllerBase
{
    private readonly IFileUploadService _fileUploadService;
    private readonly ILogger<FileUploadController> _logger;

    public FileUploadController(
        IFileUploadService fileUploadService,
        ILogger<FileUploadController> logger)
    {
        _fileUploadService = fileUploadService ?? throw new ArgumentNullException(nameof(fileUploadService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
    private long GetUserUnitId() => long.TryParse(User.FindFirst("unit_id")?.Value, out var unitId) ? unitId : 0;

    // ===== DIRECT UPLOAD (File ≤ 10MB) =====

    /// <summary>
    /// Upload file trực tiếp (dùng cho file ≤ 10MB)
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10_485_760)]  // 10MB limit at controller level
    public async Task<IActionResult> DirectUploadFile(
        [FromForm] IFormFile file,
        [FromForm] Guid folderId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("File không được để trống");

            var userUnitId = GetUserUnitId();
            if (userUnitId == 0)
                return Unauthorized("Không thể xác định đơn vị của người dùng");

            // Use 'using' to ensure stream is disposed automatically
            using (var fileStream = file.OpenReadStream())
            {
                var result = await _fileUploadService.UploadFileDirectAsync(
                    fileStream,
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    folderId,
                    UserId,
                    userUnitId,
                    cancellationToken);

                return Ok(result);
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error for upload");
            return BadRequest(new { code = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Operation error for upload");
            return BadRequest(new { code = "OPERATION_ERROR", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Permission denied for upload");
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            return StatusCode(500, new { code = "UPLOAD_ERROR", message = ex.Message });
        }
    }

    // ===== CHUNKED UPLOAD (File > 10MB) =====

    /// <summary>
    /// Khởi tạo chunked upload
    /// </summary>
    [HttpPost("initiate-chunked")]
    public async Task<IActionResult> InitiateChunkedUpload(
        [FromBody] InitiateChunkedUploadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userUnitId = GetUserUnitId();
            if (userUnitId == 0)
                return Unauthorized("Không thể xác định đơn vị của người dùng");

            var result = await _fileUploadService.InitiateChunkedUploadAsync(
                request.FileName,
                request.FileSize,
                request.FolderId,
                UserId,
                userUnitId,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error initiating chunked upload");
            return BadRequest(new { code = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Operation error initiating chunked upload");
            return BadRequest(new { code = "OPERATION_ERROR", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Permission denied for chunked upload");
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating chunked upload");
            return StatusCode(500, new { code = "INITIATE_ERROR", message = ex.Message });
        }
    }

    /// <summary>
    /// Upload chunk
    /// </summary>
    [HttpPut("{uploadId}/chunks/{chunkNumber}")]
    [RequestSizeLimit(26_214_400)]  // 25MB limit for individual chunk
    public async Task<IActionResult> UploadChunk(
        [FromRoute] string uploadId,
        [FromRoute] int chunkNumber,
        [FromBody] byte[] chunkData,
        CancellationToken cancellationToken)
    {
        try
        {
            // Convert byte array to stream for service layer
            using (var chunkStream = new MemoryStream(chunkData))
            {
                var eTag = await _fileUploadService.UploadChunkAsync(
                    uploadId,
                    chunkNumber,
                    chunkStream,
                    chunkData.Length,
                    cancellationToken);

                return Ok(new { chunkNumber, eTag });
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Operation error uploading chunk {ChunkNumber}", chunkNumber);
            return BadRequest(new { code = "OPERATION_ERROR", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading chunk {ChunkNumber} for session {UploadId}", chunkNumber, uploadId);
            return StatusCode(500, new { code = "CHUNK_UPLOAD_ERROR", message = ex.Message });
        }
    }

    /// <summary>
    /// Hoàn tất chunked upload
    /// </summary>
    [HttpPost("{uploadId}/complete")]
    public async Task<IActionResult> CompleteChunkedUpload(
        [FromRoute] string uploadId,
        [FromBody] CompleteChunkedUploadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _fileUploadService.CompleteChunkedUploadAsync(
                uploadId,
                request,
                UserId,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Operation error completing chunked upload {UploadId}", uploadId);
            return BadRequest(new { code = "OPERATION_ERROR", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing chunked upload {UploadId}", uploadId);
            return StatusCode(500, new { code = "COMPLETE_ERROR", message = ex.Message });
        }
    }
}
