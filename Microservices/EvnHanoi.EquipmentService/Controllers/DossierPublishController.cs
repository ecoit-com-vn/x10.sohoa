using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// API xuất bản hồ sơ — tách khỏi DossierController.
/// Quyền: DOSSIER_PUBLISH_RELEASE (gom publish / unpublish / republish).
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
