using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.Infrastructure.Audit;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// API tab Tài liệu đính kèm — partial của DossierControllerBase.
/// </summary>
public abstract partial class DossierControllerBase
{
    private long GetUserUnitId()
    {
        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        return long.TryParse(unitIdClaim, out var unitId) ? unitId : 0;
    }

    [HttpGet("{id:guid}/documents")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDocuments(
        Guid id,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var filter = new DossierDocumentFilterDto
            {
                Keyword = keyword,
                Page = page,
                PageSize = pageSize
            };
            var (items, totalCount) = await _dossierDocumentService.GetDocumentsAsync(id, filter);
            return Ok(new { items, totalCount, page, pageSize });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Loại văn bản gắn với loại hồ sơ của dossier — dùng combobox tab Tài liệu.
    /// Map quyền DOSSIER_VIEW / DOSSIER_DIGITIZATION_VIEW (không dùng catalog/dossier-type).
    /// </summary>
    [HttpGet("{id:guid}/document-types")]
    public async Task<IActionResult> GetDocumentTypes(Guid id)
    {
        try
        {
            var items = await _dossierDocumentService.GetDocumentTypesForDossierAsync(id);
            return Ok(items);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/documents/{versionId:guid}/download-url")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDocumentDownloadUrl(
        Guid id,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _dossierDocumentService.GetDownloadTokenAsync(id, versionId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/documents/upload")]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> UploadDocument(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] Guid documentTypeId,
        [FromForm] int uploadSource = 3,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File không được để trống" });

        if (documentTypeId == Guid.Empty)
            return BadRequest(new { message = "Loại văn bản (DocumentType) là bắt buộc." });

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _dossierDocumentService.UploadDirectAsync(
                id,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                documentTypeId,
                uploadSource,
                UserId,
                GetUserUnitId(),
                UserFullName,
                cancellationToken);
            HttpContext.SetAudit(id.ToString(), file.FileName, $"Upload tài liệu vào hồ sơ {id}: {file.FileName}", "DOCUMENT", AuditActions.Import);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/documents/upload/chunked/initiate")]
    public async Task<IActionResult> InitiateDocumentChunkedUpload(
        Guid id,
        [FromBody] InitiateDossierChunkedUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        try
        {
            var result = await _dossierDocumentService.InitiateChunkedUploadAsync(
                id, request.FileName, request.FileSize, UserId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/documents/upload/chunked/{uploadId}/chunks/{chunkNumber:int}")]
    [RequestSizeLimit(26_214_400)]
    public async Task<IActionResult> UploadDocumentChunk(
        Guid id,
        string uploadId,
        int chunkNumber,
        [FromBody] byte[] chunkData,
        CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new MemoryStream(chunkData);
            var eTag = await _dossierDocumentService.UploadChunkAsync(
                id, uploadId, chunkNumber, stream, chunkData.Length, UserId, GetUserUnitId(), cancellationToken);
            return Ok(new { chunkNumber, eTag });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{id:guid}/documents/upload/chunked/{uploadId}/complete")]
    public async Task<IActionResult> CompleteDocumentChunkedUpload(
        Guid id,
        string uploadId,
        [FromBody] CompleteChunkedUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        try
        {
            var result = await _dossierDocumentService.CompleteChunkedUploadAsync(
                id, uploadId, request, UserId, GetUserUnitId(), UserFullName, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/documents/move-from-folder")]
    public async Task<IActionResult> UploadDocumentsFromFolder(
        Guid id,
        [FromBody] MoveDocumentsFromFolderRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        try
        {
            var moved = await _dossierDocumentService.MoveFromFolderAsync(
                id, request, UserId, GetUserUnitId(), cancellationToken);
            return Ok(new
            {
                success = true,
                movedCount = moved.Count,
                movedNames = moved.Select(m => m.Name).ToList(),
                movedDocuments = moved
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _dossierDocumentService.DeleteDocumentAsync(id, documentId, UserId, cancellationToken);
            return deleted ? NoContent() : NotFound(new { message = "Không tìm thấy tài liệu trong hồ sơ." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/documents/{versionId:guid}/form-template")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDocumentFormTemplate(Guid id, Guid versionId)
    {
        try
        {
            var template = await _dossierDocumentService.GetFormTemplateForDocumentVersionAsync(id, versionId);
            if (template is null)
                return NotFound(new { message = "Không tìm thấy biểu mẫu EAV cho tài liệu này." });

            return Ok(template);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/documents/{versionId:guid}/digitization")]
    public async Task<IActionResult> SubmitDocumentOCRDigitization(
        Guid id,
        Guid versionId,
        [FromBody] SubmitDossierDocumentDigitizationRequest? request)
    {
        try
        {
            var result = await _documentDigitizationService.SubmitForDossierDocumentAsync(
                id,
                versionId,
                request ?? new SubmitDossierDocumentDigitizationRequest(),
                UserId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = ex.Message });
        }
    }

    /// <summary>Bóc tách lại — gửi thẳng ExtractionWorker, form EAV luôn load mới.</summary>
    [HttpPost("{id:guid}/documents/{versionId:guid}/digitization/re-extract")]
    public async Task<IActionResult> ReExtractDocument(
        Guid id,
        Guid versionId)
    {
        try
        {
            var result = await _documentDigitizationService.ReExtractForDossierDocumentAsync(
                id,
                versionId,
                UserId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/documents/{versionId:guid}/digitization/progress")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDocumentDigitizationProgress(Guid id, Guid versionId)
    {
        try
        {
            var progress = await _documentDigitizationService.GetProgressForDossierAsync(id, versionId);
            if (progress == null)
                return NotFound(new { message = "Chưa có tiến trình OCR cho phiên bản tài liệu này." });

            return Ok(progress);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/documents/{versionId:guid}/digitization/result")]
    public async Task<IActionResult> SaveDocumentDigitizationResult(
        Guid id,
        Guid versionId,
        [FromBody] SaveDocumentExtractionDataRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        try
        {
            var result = await _documentDigitizationService.SaveDocumentExtractionDataAsync(
                id,
                versionId,
                request,
                UserId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/documents/{versionId:guid}/digitization/result")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDocumentDigitizationResult(Guid id, Guid versionId)
    {
        try
        {
            var result = await _documentDigitizationService.GetExtractionResultForDossierAsync(id, versionId);
            if (result == null)
                return NotFound(new { message = "Chưa có kết quả bóc tách cho phiên bản tài liệu này." });

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Không thể lấy kết quả bóc tách tài liệu." });
        }
    }

    [HttpPost("{id:guid}/documents/versions/{versionId:guid}/rollback")]
    public async Task<IActionResult> RollbackDocumentVersion(
        Guid id,
        Guid versionId)
    {
        try
        {
            var result = await _dossierDocumentService.RollbackDocumentVersionAsync(id, versionId, UserId, GetUserUnitId());
            if (!result)
                return BadRequest(new { message = "Khôi phục phiên bản thất bại" });

            return Ok(new { message = "Khôi phục phiên bản thành công" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/documents/versions/{versionId:guid}")]
    public async Task<IActionResult> DeleteDocumentVersion(
        Guid id,
        Guid versionId)
    {
        try
        {
            var result = await _dossierDocumentService.DeleteDocumentVersionAsync(id, versionId, UserId);
            if (!result)
                return BadRequest(new { message = "Xóa phiên bản thất bại" });

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
