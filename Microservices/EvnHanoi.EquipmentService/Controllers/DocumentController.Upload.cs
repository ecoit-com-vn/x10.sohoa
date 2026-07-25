using Microsoft.AspNetCore.Mvc;
using EvnHanoi.EquipmentService.Core.DTOs;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Upload tài liệu vào kho thư mục — quyền DOCUMENT_IMPORT (không dùng /api/v1/files).
/// </summary>
public partial class DocumentController
{
    [HttpPost("upload")]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> UploadFileToFolder(
        [FromForm] IFormFile file,
        [FromForm] Guid folderId,
        CancellationToken cancellationToken,
        [FromForm] int uploadSource = 3)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("File không được để trống");

            var userUnitId = GetUserUnitId();
            if (userUnitId == 0)
                return Unauthorized("Không thể xác định đơn vị của người dùng");

            using var fileStream = file.OpenReadStream();
            var result = await _fileUploadService.UploadFileDirectAsync(
                fileStream,
                file.FileName,
                file.ContentType,
                file.Length,
                folderId,
                uploadSource,
                UserId,
                userUnitId,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error for document upload");
            return BadRequest(new { code = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Operation error for document upload");
            return BadRequest(new { code = "OPERATION_ERROR", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Permission denied for document upload");
            return StatusCode(403, new { code = "FORBIDDEN", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document to folder");
            return StatusCode(500, new { code = "UPLOAD_ERROR", message = ex.Message });
        }
    }

    [HttpPost("upload/chunked/initiate")]
    public async Task<IActionResult> InitiateFolderChunkedUpload(
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
            _logger.LogWarning(ex, "Validation error initiating document chunked upload");
            return BadRequest(new { code = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Operation error initiating document chunked upload");
            return BadRequest(new { code = "OPERATION_ERROR", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Permission denied for document chunked upload");
            return StatusCode(403, new { code = "FORBIDDEN", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating document chunked upload");
            return StatusCode(500, new { code = "INITIATE_ERROR", message = ex.Message });
        }
    }

    [HttpPut("upload/chunked/{uploadId}/chunks/{chunkNumber:int}")]
    [RequestSizeLimit(26_214_400)]
    public async Task<IActionResult> UploadFolderChunk(
        [FromRoute] string uploadId,
        [FromRoute] int chunkNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var contentLength = Request.ContentLength ?? 0;
            if (contentLength <= 0)
                return BadRequest("Dữ liệu chunk không được để trống");

            var eTag = await _fileUploadService.UploadChunkAsync(
                uploadId,
                chunkNumber,
                Request.Body,
                contentLength,
                cancellationToken);

            return Ok(new { chunkNumber, eTag });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Operation error uploading document chunk {ChunkNumber}", chunkNumber);
            return BadRequest(new { code = "OPERATION_ERROR", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document chunk {ChunkNumber} for session {UploadId}", chunkNumber, uploadId);
            return StatusCode(500, new { code = "CHUNK_UPLOAD_ERROR", message = ex.Message });
        }
    }

    [HttpPost("upload/chunked/{uploadId}/complete")]
    public async Task<IActionResult> CompleteFolderChunkedUpload(
        [FromRoute] string uploadId,
        [FromBody] CompleteChunkedUploadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userUnitId = GetUserUnitId();
            if (userUnitId == 0)
                return Unauthorized("Không thể xác định đơn vị của người dùng");

            var result = await _fileUploadService.CompleteChunkedUploadAsync(
                uploadId,
                request,
                UserId,
                userUnitId,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Operation error completing document chunked upload {UploadId}", uploadId);
            return BadRequest(new { code = "OPERATION_ERROR", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing document chunked upload {UploadId}", uploadId);
            return StatusCode(500, new { code = "COMPLETE_ERROR", message = ex.Message });
        }
    }

    [HttpDelete("upload/chunked/{uploadId}/abort")]
    public async Task<IActionResult> AbortFolderChunkedUpload(
        [FromRoute] string uploadId,
        CancellationToken cancellationToken)
    {
        try
        {
            var userUnitId = GetUserUnitId();
            if (userUnitId == 0)
                return Unauthorized("Không thể xác định đơn vị của người dùng");

            await _fileUploadService.AbortChunkedUploadAsync(
                uploadId,
                UserId,
                userUnitId,
                cancellationToken);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aborting document chunked upload {UploadId}", uploadId);
            return StatusCode(500, new { code = "ABORT_ERROR", message = ex.Message });
        }
    }
}
