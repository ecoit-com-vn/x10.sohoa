using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Security;
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
    [BypassDynamicPermission]
    public async Task<IActionResult> GetCatalogTree()
    {
        var unitId = GetUserUnitId();
        if (unitId == 0)
            return Unauthorized("Không thể xác định đơn vị của người dùng");

        try
        {
            var nodes = await _documentService.GetDossierCatalogTreeAsync(unitId);
            return Ok(nodes);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("documents")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] string? folderId,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var unitId = GetUserUnitId();
        if (unitId == 0)
            return Unauthorized("Không thể xác định đơn vị của người dùng");

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var (items, totalCount) = await _documentService.GetDossierCatalogDocumentsAsync(unitId, folderId, keyword, page, pageSize);

        return Ok(new { items, totalCount, page, pageSize });
    }
}
