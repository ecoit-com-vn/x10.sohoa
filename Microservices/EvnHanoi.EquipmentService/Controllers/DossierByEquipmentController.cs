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
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IDossierDocumentService _dossierDocumentService;
    private readonly IDocumentDigitizationService _documentDigitizationService;

    public DossierByEquipmentController(
        IDossierService dossierService,
        IEquipmentRepository equipmentRepository,
        IDossierDocumentService dossierDocumentService,
        IDocumentDigitizationService documentDigitizationService)
    {
        _dossierService = dossierService ?? throw new ArgumentNullException(nameof(dossierService));
        _equipmentRepository = equipmentRepository ?? throw new ArgumentNullException(nameof(equipmentRepository));
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

    /// <summary>
    /// Trả về danh sách thiết bị liên quan của hồ sơ đã xuất bản.
    /// </summary>
    [HttpGet("{id:guid}/equipments")]
    public async Task<IActionResult> GetRelatedEquipments(Guid id)
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

    /// <summary>
    /// Chi tiết thiết bị chỉ đọc trong ngữ cảnh Tra cứu hồ sơ thiết bị.
    /// Chỉ trả dữ liệu khi hồ sơ đã xuất bản thuộc phạm vi đơn vị của người dùng
    /// và thiết bị thực sự được gắn với hồ sơ đó.
    /// </summary>
    [HttpGet("{id:guid}/equipments/{equipmentId:guid}")]
    public async Task<IActionResult> GetRelatedEquipmentDetail(Guid id, Guid equipmentId)
    {
        var (isAdmin, unitId) = ResolveUserScope();
        if (!isAdmin && unitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var dossier = await _dossierService.GetPublishedDetailByIdAsync(id, isAdmin, unitId);
        if (dossier is null)
            return NotFound(new { message = $"Không tìm thấy hồ sơ đã xuất bản với ID = {id}" });

        var relatedEquipments = await _dossierService.GetEquipmentsAsync(id);
        if (!relatedEquipments.Any(item => item.EquipmentId == equipmentId))
            return NotFound(new { message = "Thiết bị không thuộc hồ sơ đang tra cứu." });

        var equipment = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        return equipment is null
            ? NotFound(new { message = "Không tìm thấy thiết bị." })
            : Ok(equipment);
    }

    /// <summary>
    /// Trả về danh sách hồ sơ liên quan (cùng trạm/đường dây) của hồ sơ đã xuất bản.
    /// </summary>
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
