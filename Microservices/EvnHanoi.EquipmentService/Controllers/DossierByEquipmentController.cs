using System.Security.Claims;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Tra cứu hồ sơ thiết bị — lookup filter + chi tiết (Oracle).
/// Danh sách hồ sơ đọc từ NotificationService /search-dossiers-by-equipment (ES).
/// Quyền: SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/dossiers-by-equipment")]
public partial class DossierByEquipmentController : ControllerBase
{
    private readonly IDossierService _dossierService;
    private readonly IDossierDocumentService _dossierDocumentService;
    private readonly IDocumentDigitizationService _documentDigitizationService;

    public DossierByEquipmentController(
        IDossierService dossierService,
        IDossierDocumentService dossierDocumentService,
        IDocumentDigitizationService documentDigitizationService)
    {
        _dossierService = dossierService ?? throw new ArgumentNullException(nameof(dossierService));
        _dossierDocumentService = dossierDocumentService ?? throw new ArgumentNullException(nameof(dossierDocumentService));
        _documentDigitizationService = documentDigitizationService ?? throw new ArgumentNullException(nameof(documentDigitizationService));
    }

    [HttpGet("infrastructures")]
    public async Task<IActionResult> GetInfrastructures([FromQuery] DossierByEquipmentFilterDto filter)
    {
        var (isAdmin, unitId) = ResolveUserScope();
        if (!isAdmin && unitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var items = await _dossierService.GetEquipmentLookupInfrastructuresAsync(filter, isAdmin, unitId);
        return Ok(items);
    }

    [HttpGet("equipment-types")]
    public async Task<IActionResult> GetEquipmentTypes([FromQuery] DossierByEquipmentFilterDto filter)
    {
        var (isAdmin, unitId) = ResolveUserScope();
        if (!isAdmin && unitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var items = await _dossierService.GetEquipmentLookupEquipmentTypesAsync(filter, isAdmin, unitId);
        return Ok(items);
    }

    [HttpGet("equipments")]
    public async Task<IActionResult> GetEquipments([FromQuery] DossierByEquipmentFilterDto filter)
    {
        var (isAdmin, unitId) = ResolveUserScope();
        if (!isAdmin && unitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var items = await _dossierService.GetEquipmentLookupEquipmentsAsync(filter, isAdmin, unitId);
        return Ok(items);
    }

    [HttpGet("dossier-types")]
    public async Task<IActionResult> GetDossierTypes([FromQuery] DossierByEquipmentFilterDto filter)
    {
        var (isAdmin, unitId) = ResolveUserScope();
        if (!isAdmin && unitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var items = await _dossierService.GetEquipmentLookupDossierTypesAsync(filter, isAdmin, unitId);
        return Ok(items);
    }

    [HttpGet("bhs-columns")]
    public async Task<IActionResult> GetBhsColumns()
    {
        var columns = await _dossierService.GetBhsCatalogColumnsAsync();
        return Ok(columns);
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
}
