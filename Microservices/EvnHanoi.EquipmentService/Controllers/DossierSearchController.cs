using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Tìm kiếm hồ sơ trong kho — cây thư mục, danh sách hồ sơ đã xuất bản, chi tiết chỉ đọc.
/// Quyền: SEARCH_DOSSIERS_IN_WAREHOUSE_VIEW.
/// </summary>
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

    private (bool IsAdmin, long? UnitId) ResolveUserScope()
    {
        var isAdmin = User.IsInRole("ADMIN") ||
                      User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");

        long? unitId = null;
        if (!isAdmin)
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            if (long.TryParse(unitIdClaim, out var userUnitId) && userUnitId > 0)
                unitId = userUnitId;
        }

        return (isAdmin, unitId);
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

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalogDossiers(
        [FromQuery] string? keyword,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] Guid? dossierTypeId,
        [FromQuery] long? unitId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var (items, totalCount) = await _dossierService.GetCatalogDossiersAsync(
            keyword, infrastructureId, dossierTypeId, unitId, page, pageSize);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("getlistdocument")]
    public async Task<IActionResult> GetListDocuments(
        [FromQuery] string? keyword,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] Guid? dossierTypeId,
        [FromQuery] long? unitId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var dossierIds = await _dossierService.GetListDocumentIdsAsync(
            keyword, infrastructureId, dossierTypeId, unitId, page, pageSize);

        var (items, totalCount) = await _documentService.GetDocumentsByDossierIdsAsync(
            dossierIds, keyword, page, pageSize);

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
        var (isAdmin, unitId) = ResolveUserScope();
        if (!isAdmin && unitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var dossier = await _dossierService.GetPublishedDetailByIdAsync(id, isAdmin, unitId);
        if (dossier == null)
            return NotFound(new { message = $"Không tìm thấy hồ sơ đã xuất bản với ID = {id}" });

        var (items, totalCount) = await _dossierService.GetCatalogDossiersAsync(
            keyword,
            dossier.InfrastructureId,
            dossierTypeId,
            null,
            page,
            pageSize);

        var filteredItems = items.Where(item => item.Id != id).ToList();
        var count = totalCount;
        if (items.Count() != filteredItems.Count)
        {
            count = Math.Max(0, totalCount - 1);
        }

        return Ok(new { items = filteredItems, totalCount = count, page, pageSize });
    }

    [HttpGet("{id:guid}/equipments")]
    public async Task<IActionResult> GetEquipments(Guid id)
    {
        var (isAdmin, unitId) = ResolveUserScope();
        if (!isAdmin && unitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var dossier = await _dossierService.GetPublishedDetailByIdAsync(id, isAdmin, unitId);
        if (dossier is null)
            return NotFound(new { message = $"Không tìm thấy hồ sơ đã xuất bản với ID = {id}" });

        var equipments = await _dossierService.GetEquipmentsAsync(id);
        return Ok(equipments);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var (isAdmin, unitId) = ResolveUserScope();
        if (!isAdmin && unitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var detail = await _dossierService.GetPublishedDetailByIdAsync(id, isAdmin, unitId);
        if (detail is null)
            return NotFound(new { message = $"Không tìm thấy hồ sơ đã xuất bản với ID = {id}" });

        return Ok(detail);
    }
}
