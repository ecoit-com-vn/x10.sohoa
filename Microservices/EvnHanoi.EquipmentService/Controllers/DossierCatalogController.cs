using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/dossiers/catalog")]
public class DossierCatalogController : ControllerBase
{
    private readonly IDocumentManagementService _documentService;

    public DossierCatalogController(IDocumentManagementService documentService)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
    }

    private long GetUserUnitId()
    {
        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        return long.TryParse(unitIdClaim, out var unitId) ? unitId : 0;
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetCatalogTree([FromQuery] long? unitId)
    {
        var targetUnitId = unitId ?? GetUserUnitId();
        if (targetUnitId == 0)
            return Unauthorized("Không thể xác định đơn vị");

        try
        {
            var nodes = await _documentService.GetDossierCatalogTreeAsync(targetUnitId);
            return Ok(nodes);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] string? folderId,
        [FromQuery] string? keyword,
        [FromQuery] long? unitId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var targetUnitId = unitId ?? GetUserUnitId();
        if (targetUnitId == 0)
            return Unauthorized("Không thể xác định đơn vị");

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var (items, totalCount) = await _documentService.GetDossierCatalogDocumentsAsync(targetUnitId, folderId, keyword, page, pageSize);

        return Ok(new { items, totalCount, page, pageSize });
    }
}
