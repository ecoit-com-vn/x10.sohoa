using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/dossiers/search")]
public class DossierSearchController : ControllerBase
{
    private readonly IDocumentManagementService _documentService;
    private readonly IDossierService _dossierService;

    public DossierSearchController(
        IDocumentManagementService documentService,
        IDossierService dossierService)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _dossierService = dossierService ?? throw new ArgumentNullException(nameof(dossierService));
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

    [HttpGet("{id:guid}/related")]
    public async Task<IActionResult> GetRelatedDossiers(
        Guid id,
        [FromQuery] string? keyword,
        [FromQuery] Guid? dossierTypeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var dossier = await _dossierService.GetDetailByIdAsync(id);
        if (dossier == null)
            return NotFound(new { message = $"Không tìm thấy hồ sơ với ID = {id}" });

        // Lấy danh sách hồ sơ có cùng InfrastructureId (trạm/đường dây)
        var (items, totalCount) = await _dossierService.GetCatalogDossiersAsync(
            keyword, 
            dossier.InfrastructureId, 
            dossierTypeId, 
            null, // Lấy toàn bộ trong trạm mà không bắt buộc trùng đơn vị con nếu trạm dùng chung
            page, 
            pageSize);

        // Loại bỏ hồ sơ hiện tại khỏi danh sách hồ sơ liên quan để tránh tự hiển thị chính nó
        var filteredItems = items.Where(item => item.Id != id).ToList();
        var count = totalCount;
        if (items.Count() != filteredItems.Count)
        {
            count = Math.Max(0, totalCount - 1);
        }

        return Ok(new { items = filteredItems, totalCount = count, page, pageSize });
    }
}
