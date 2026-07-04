using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// API xuất bản hồ sơ — tách khỏi DossierController.
/// GET → DOSSIER_PUBLISH_VIEW; PUT publish/unpublish/republish → DOSSIER_PUBLISH_RELEASE.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/dossier-publish")]
public class DossierPublishController : ControllerBase
{
    private readonly IDossierService _dossierService;

    public DossierPublishController(IDossierService dossierService)
    {
        _dossierService = dossierService ?? throw new ArgumentNullException(nameof(dossierService));
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? User.Identity?.Name ?? "system";

    /// <summary>Chi tiết hồ sơ trong menu xuất bản (Oracle).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var detail = await _dossierService.GetDetailByIdAsync(id);
        if (detail == null)
            return NotFound(new { message = $"Không tìm thấy hồ sơ với ID = {id}" });

        if (detail.StatusId != DossierStatusConstants.Approved)
            return StatusCode(403, new { message = "Hồ sơ chưa hoàn thành quy trình phê duyệt, không thể xem trong menu xuất bản." });

        return Ok(detail);
    }

    /// <summary>Biểu mẫu EAV theo ngữ cảnh xuất bản — tránh gọi eav-form-templates (quyền FORM).</summary>
    [HttpGet("{id:guid}/form-template")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetFormTemplate(Guid id, [FromQuery] Guid? formId = null)
    {
        try
        {
            var detail = await _dossierService.GetDetailByIdAsync(id);
            if (detail == null)
                return NotFound(new { message = $"Không tìm thấy hồ sơ với ID = {id}" });

            if (detail.StatusId != DossierStatusConstants.Approved)
                return StatusCode(403, new { message = "Hồ sơ chưa hoàn thành quy trình phê duyệt." });

            var template = await _dossierService.GetFormTemplateForDossierAsync(id, formId);
            if (template is null)
                return NotFound(new { message = "Không tìm thấy biểu mẫu EAV cho hồ sơ này." });

            return Ok(template);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        try
        {
            var success = await _dossierService.UpdatePublishStatusAsync(id, 2, UserId);
            if (!success) return BadRequest(new { message = "Không thể xuất bản hồ sơ." });
            return Ok(new { success = true, publishStatusId = 2 });
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

    [HttpPut("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        try
        {
            var success = await _dossierService.UpdatePublishStatusAsync(id, 3, UserId);
            if (!success) return BadRequest(new { message = "Không thể hủy xuất bản hồ sơ." });
            return Ok(new { success = true, publishStatusId = 3 });
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

    [HttpPut("{id:guid}/republish")]
    public async Task<IActionResult> Republish(Guid id)
    {
        try
        {
            var success = await _dossierService.UpdatePublishStatusAsync(id, 2, UserId);
            if (!success) return BadRequest(new { message = "Không thể tái xuất bản hồ sơ." });
            return Ok(new { success = true, publishStatusId = 2 });
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
