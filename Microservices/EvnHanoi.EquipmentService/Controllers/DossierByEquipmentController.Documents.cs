using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Tab tài liệu tra cứu hồ sơ thiết bị — chỉ đọc, quyền SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW.
/// </summary>
public partial class DossierByEquipmentController
{
    [HttpGet("{id:guid}/documents")]
    public async Task<IActionResult> GetDocuments(
        Guid id,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var access = await EnsurePublishedDossierAccessAsync(id);
        if (access is not null)
            return access;

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

    [HttpGet("{id:guid}/documents/{versionId:guid}/download-url")]
    public async Task<IActionResult> GetDocumentDownloadUrl(
        Guid id,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var access = await EnsurePublishedDossierAccessAsync(id);
        if (access is not null)
            return access;

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

    [HttpGet("{id:guid}/documents/{versionId:guid}/digitization/progress")]
    public async Task<IActionResult> GetDocumentDigitizationProgress(Guid id, Guid versionId)
    {
        var access = await EnsurePublishedDossierAccessAsync(id);
        if (access is not null)
            return access;

        try
        {
            var progress = await _documentDigitizationService.GetProgressForDossierAsync(id, versionId);
            if (progress is null)
                return NotFound(new { message = "Chưa có tiến trình OCR cho phiên bản tài liệu này." });

            return Ok(progress);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/documents/{versionId:guid}/digitization/result")]
    public async Task<IActionResult> GetDocumentDigitizationResult(Guid id, Guid versionId)
    {
        var access = await EnsurePublishedDossierAccessAsync(id);
        if (access is not null)
            return access;

        try
        {
            var result = await _documentDigitizationService.GetExtractionResultForDossierAsync(id, versionId);
            if (result is null)
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

    private async Task<IActionResult?> EnsurePublishedDossierAccessAsync(Guid dossierId)
    {
        var (isAdmin, unitId) = ResolveUserScope();
        if (!isAdmin && unitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        if (!await _dossierService.IsPublishedDossierAccessibleAsync(dossierId, isAdmin, unitId))
            return NotFound(new { message = $"Không tìm thấy hồ sơ đã xuất bản với ID = {dossierId}" });

        return null;
    }
}
