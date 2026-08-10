using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.Infrastructure.Audit;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
public abstract partial class DossierControllerBase : ControllerBase
{
    private readonly IDossierService _dossierService;
    private readonly IDossierDocumentService _dossierDocumentService;
    private readonly IDocumentDigitizationService _documentDigitizationService;
    private readonly DossierKindGuard _kindGuard;
    private readonly IAuditPublisher _auditPublisher;
    private readonly AuditServiceMetadata _auditServiceMetadata;

    protected abstract int ExpectedKindId { get; }

    protected DossierControllerBase(
        IDossierService dossierService,
        IDossierDocumentService dossierDocumentService,
        IDocumentDigitizationService documentDigitizationService,
        DossierKindGuard kindGuard,
        IAuditPublisher auditPublisher,
        AuditServiceMetadata auditServiceMetadata)
    {
        _dossierService = dossierService ?? throw new ArgumentNullException(nameof(dossierService));
        _dossierDocumentService = dossierDocumentService ?? throw new ArgumentNullException(nameof(dossierDocumentService));
        _documentDigitizationService = documentDigitizationService ?? throw new ArgumentNullException(nameof(documentDigitizationService));
        _kindGuard = kindGuard ?? throw new ArgumentNullException(nameof(kindGuard));
        _auditPublisher = auditPublisher ?? throw new ArgumentNullException(nameof(auditPublisher));
        _auditServiceMetadata = auditServiceMetadata ?? throw new ArgumentNullException(nameof(auditServiceMetadata));
    }

    private async Task<IActionResult?> EnsureKindAsync(Guid id)
    {
        try
        {
            await _kindGuard.EnsureAsync(id, ExpectedKindId);
            return null;
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? User.Identity?.Name ?? "system";

    private string UserName => User.FindFirst("preferred_username")?.Value
                             ?? User.FindFirst(ClaimTypes.Name)?.Value
                             ?? User.Identity?.Name ?? "system";

    private string UserFullName => User.FindFirst("name")?.Value
                                 ?? User.FindFirst(ClaimTypes.GivenName)?.Value
                                 ?? UserName;

    // ===== DANH SÁCH =====

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] int? gridTypeId,
        [FromQuery] long? unitId,
        [FromQuery] int? statusId,
        [FromQuery] Guid? dossierTypeId,
        [FromQuery] Guid? equipmentId,
        [FromQuery] string? tab,
        [FromQuery] string? menuScope,
        [FromQuery] int? kindId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var filter = new DossierFilterDto
        {
            Keyword = string.IsNullOrWhiteSpace(keyword)
        ? null
        : keyword.Trim(),

            InfrastructureId = infrastructureId,
            GridTypeId = gridTypeId,
            UnitId = unitId,
            StatusId = statusId,
            DossierTypeId = dossierTypeId,
            EquipmentId = equipmentId,
            Page = page,
            PageSize = pageSize,
            Tab = tab,
            MenuScope = menuScope,
            KindId = kindId ?? ExpectedKindId,
            UserId = UserId
        };

        var (items, totalCount) = await _dossierService.GetPagedAsync(filter);
        return Ok(new { items, totalCount, page, pageSize });
    }

    /// <summary>
    /// Danh sách hồ sơ đã xuất bản theo hộp — dùng bởi caller cũ; ưu tiên GET /api/v1/dossiers/search/catalog.
    /// </summary>
    [HttpGet("catalog")]
    [BypassDynamicPermission]
    [Obsolete("Dùng GET /api/v1/dossiers/search/catalog (SEARCH_DOSSIERS_IN_WAREHOUSE_VIEW).")]
    public async Task<IActionResult> GetCatalogDossiers(
        [FromQuery] string? keyword,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] Guid? dossierTypeId,
        [FromQuery] long? unitId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, totalCount) = await _dossierService.GetCatalogDossiersAsync(
            keyword, infrastructureId, dossierTypeId, unitId, page, pageSize);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("grid-types/lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetGridTypesLookup()
    {
        var items = await _dossierService.GetGridTypesLookupAsync();
        return Ok(items);
    }

    [HttpGet("infrastructures/lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetInfrastructuresLookup()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        long? userUnitId = null;
        if (!isAdmin)
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            if (!string.IsNullOrEmpty(unitIdClaim) && long.TryParse(unitIdClaim, out var parsedUnitId))
            {
                userUnitId = parsedUnitId;
            }
        }

        var items = await _dossierService.GetInfrastructuresLookupAsync(
            isAdmin,
            userUnitId,
            GetAuthorizedUnitIds());
        return Ok(items);
    }

    /// <summary>
    /// Cây kho lưu trữ (kệ → tầng → hộp) — chỉ đúng đơn vị hiện tại, không gồm đơn vị con.
    /// </summary>
    [HttpGet("physical-storage/tree")]
    public async Task<IActionResult> GetPhysicalStorageTree([FromQuery] long? unitId = null)
    {
        long? currentUnitId = unitId is > 0 ? unitId : null;
        if (currentUnitId is null)
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            if (!string.IsNullOrEmpty(unitIdClaim) && long.TryParse(unitIdClaim, out var parsedUnitId) && parsedUnitId > 0)
                currentUnitId = parsedUnitId;
        }

        var items = await _dossierService.GetPhysicalStorageTreeAsync(currentUnitId);
        return Ok(items);
    }

    [HttpGet("dossier-type/lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDossierTypesLookup()
    {
        var items = await _dossierService.GetDossierTypesLookupAsync();
        return Ok(items);
    }

    /// <summary>Lookup nhóm hồ sơ (DOSSIER_GROUPS) — dùng cho form tạo/sửa.</summary>
    [HttpGet("dossier-groups/lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDossierGroupsLookup()
    {
        var items = await _dossierService.GetDossierGroupsLookupAsync();
        return Ok(items);
    }

    [HttpGet("equipment/lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetEquipmentLookup(
        [FromQuery] string? keyword,
        [FromQuery] string? code,
        [FromQuery] string? name,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] int? gridTypeId,
        [FromQuery] long? unitId,
        [FromQuery] bool? isActive = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var filter = new EquipmentLookupFilterDto
        {
            Keyword = keyword,
            Code = code,
            Name = name,
            InfrastructureId = infrastructureId,
            GridTypeId = gridTypeId,
            UnitId = unitId,
            IsActive = isActive,
            Page = page,
            PageSize = pageSize
        };

        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        long? userUnitId = null;
        if (!isAdmin)
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            if (!string.IsNullOrEmpty(unitIdClaim) && long.TryParse(unitIdClaim, out var parsedUnitId))
            {
                userUnitId = parsedUnitId;
            }
        }

        var (items, totalCount) = await _dossierService.GetEquipmentLookupAsync(
            filter,
            isAdmin,
            userUnitId,
            GetAuthorizedUnitIds());

        return Ok(new { items, totalCount, page = filter.Page, pageSize = filter.PageSize });
    }

    // ===== CHI TIẾT =====

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var kindError = await EnsureKindAsync(id);
        if (kindError != null) return kindError;

        var detail = await _dossierService.GetDetailByIdAsync(id);
        if (detail == null) return NotFound(new { message = $"Không tìm thấy hồ sơ với ID = {id}" });
        return Ok(detail);
    }

    // ===== TẠO MỚI =====

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DossierCreateDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        try
        {
            var newId = await _dossierService.CreateAsync(dto, UserId, UserName, UserFullName, ExpectedKindId);
            HttpContext.SetAudit(resourceId: newId.ToString(), resourceType: "DOSSIER", action: AuditActions.Create);
            return CreatedAtAction(nameof(GetDetail), new { id = newId }, new { id = newId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ===== CẬP NHẬT =====

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DossierUpdateDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        var kindError = await EnsureKindAsync(id);
        if (kindError != null) return kindError;

        try
        {
            await _dossierService.UpdateAsync(id, dto, UserId);
            HttpContext.SetAudit(resourceId: id.ToString(), resourceType: "DOSSIER", action: AuditActions.Update);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex) when (ex.Message.Contains("Concurrency"))
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ===== XÓA =====

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var kindError = await EnsureKindAsync(id);
        if (kindError != null) return kindError;

        try
        {
            var detail = await _dossierService.GetDetailByIdAsync(id);
            await _dossierService.DeleteAsync(id, UserId);
            var dossierCode = ExtractDossierCodeFromFormData(detail?.FormDataJson);
            HttpContext.SetAudit(
                resourceId: id.ToString(),
                resourceName: dossierCode,
                resourceType: "DOSSIER",
                action: AuditActions.Delete);
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

    [HttpPut("{id:guid}/complete-input")]
    public async Task<IActionResult> CompleteInput(Guid id)
    {
        var kindError = await EnsureKindAsync(id);
        if (kindError != null) return kindError;

        try
        {
            var success = await _dossierService.CompleteInputAsync(id, UserId);
            if (!success) return BadRequest(new { message = "Không thể cập nhật trạng thái hồ sơ." });
            return Ok(new { success = true, message = "Xác nhận hoàn thành nhập liệu thành công." });
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

    // ===== GỬI DUYỆT / WORKFLOW =====
    // Đã chuyển sang WorkflowService: POST/GET /api/v1/dossiers-workflow/*
    // (submit, move, get-workflow-by-entity, get-workflow-history,
    //  get-workflow-definition, get-my-tasks).

    // ===== LƯU FORM DATA =====

    [HttpPost("{id:guid}/form-data")]
    public async Task<IActionResult> SaveFormData(Guid id, [FromBody] DossierSaveFormDataDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        var kindError = await EnsureKindAsync(id);
        if (kindError != null) return kindError;

        try
        {
            var result = await _dossierService.SaveFormDataAsync(id, dto, UserId);
            return Ok(new { success = true, data = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex) when (ex.Message.Contains("Concurrency"))
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ===== PHIÊN BẢN =====

    [HttpGet("{id:guid}/versions")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetVersions(Guid id)
    {
        var versions = await _dossierService.GetVersionsAsync(id);
        return Ok(versions);
    }

    // ===== THIẾT BỊ =====

    [HttpGet("{id:guid}/equipment")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetEquipments(Guid id)
    {
        var equipments = await _dossierService.GetEquipmentsAsync(id);
        return Ok(equipments);
    }

    [HttpPost("{id:guid}/equipment")]
    public async Task<IActionResult> AddEquipment(Guid id, [FromBody] AddDossierEquipmentDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });
        var result = await _dossierService.AddEquipmentAsync(id, dto.EquipmentId);
        return result ? Ok(new { success = true }) : BadRequest(new { message = "Thêm thiết bị thất bại." });
    }

    [HttpDelete("{id:guid}/equipment/{equipmentId:guid}")]
    public async Task<IActionResult> RemoveEquipment(Guid id, Guid equipmentId)
    {
        var result = await _dossierService.RemoveEquipmentAsync(id, equipmentId);
        return result ? NoContent() : NotFound(new { message = "Không tìm thấy thiết bị trong hồ sơ." });
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
            null, 
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

    [HttpGet("by-equipment/{equipmentId:guid}")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDossiersByEquipment(
        Guid equipmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (items, totalCount, columns) = await _dossierService.GetDossiersByEquipmentAsync(equipmentId, page, pageSize);
        return Ok(new { items, totalCount, columns, page, pageSize });
    }

    private List<long>? GetAuthorizedUnitIds()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        if (isAdmin)
        {
            return null;
        }

        var unitRolesClaim = User.FindFirst("unit_roles")?.Value;
        if (string.IsNullOrEmpty(unitRolesClaim))
        {
            return new List<long>();
        }

        try
        {
            var unitRoles = JsonSerializer.Deserialize<List<UnitRoleDto>>(unitRolesClaim, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return unitRoles?.Select(ur => ur.UnitId).Distinct().ToList() ?? new List<long>();
        }
        catch
        {
            return new List<long>();
        }
    }

    private class UnitRoleDto
    {
        public long UnitId { get; set; }
        public long RoleId { get; set; }
        public string RoleCode { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }
    /// <summary>
    /// Mã hồ sơ (CODE) là dữ liệu EAV động trong FormDataJson, không phải cột cố định trên bảng DOSSIERS.
    /// Dùng cho audit log Xóa hồ sơ — nơi response không có body để tự động dò như Create/Update.
    /// </summary>
    private static string? ExtractDossierCodeFromFormData(string? formDataJson)
    {
        if (string.IsNullOrWhiteSpace(formDataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(formDataJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var property in root.EnumerateObject())
            {
                if (!string.Equals(property.Name, "CODE", StringComparison.OrdinalIgnoreCase))
                    continue;

                var code = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.ToString(),
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(code))
                    return code;
            }
        }
        catch (JsonException)
        {
            // FormDataJson không hợp lệ — bỏ qua, fallback ResourceId.
        }

        return null;
    }

}
