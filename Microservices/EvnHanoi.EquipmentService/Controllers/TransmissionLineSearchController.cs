using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

using Infrastructure = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;

[Authorize]
[ApiController]
[Route("api/catalog/transmission-line-search")]
public class TransmissionLineSearchController : ControllerBase
{
    private readonly IInfrastructureRepository _infrastructureRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private const int INFRA_TYPE_ID = 2; // 2 = Transmission Line (Đường dây)

    public TransmissionLineSearchController(IInfrastructureRepository infrastructureRepository, IEquipmentRepository equipmentRepository)
    {
        _infrastructureRepository = infrastructureRepository;
        _equipmentRepository = equipmentRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] int? status = null,
        [FromQuery] long? unitId = null,
        [FromQuery] int? gridTypeId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        var (items, totalCount) = await _infrastructureRepository.GetPagedAsync(page, pageSize, INFRA_TYPE_ID, keyword, status, allowedUnitIds, unitId, gridTypeId);

        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _infrastructureRepository.GetByIdAsync(id);
        if (result == null || result.InfraTypeId != INFRA_TYPE_ID)
            return NotFound();
        return Ok(result);
    }

    [HttpGet("{id:guid}/equipments")]
    public async Task<IActionResult> GetEquipments(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] Guid? equipmentTypeId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var line = await _infrastructureRepository.GetByIdAsync(id);
        if (line == null || line.InfraTypeId != INFRA_TYPE_ID)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!line.UnitId.HasValue || !allowedUnitIds.Contains(line.UnitId.Value)))
        {
            return Forbid();
        }

        var (items, totalCount) = await _equipmentRepository.GetPagedAsync(
            page,
            pageSize,
            keyword,
            null, // code
            null, // name
            null, // unitId
            id, // infrastructureId
            null, // gridTypeId
            equipmentTypeId,
            null, // isActive
            allowedUnitIds);

        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("equipments/{equipmentId:guid}")]
    public async Task<IActionResult> GetEquipmentById(Guid equipmentId)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        if (dto == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && dto.UnitId.HasValue && !allowedUnitIds.Contains(dto.UnitId.Value))
        {
            return Forbid();
        }

        return Ok(dto);
    }

    [HttpGet("equipments/{equipmentId:guid}/form-template")]
    public async Task<IActionResult> GetEquipmentFormTemplate(Guid equipmentId)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        if (dto == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && dto.UnitId.HasValue && !allowedUnitIds.Contains(dto.UnitId.Value))
            return Forbid();

        if (string.IsNullOrWhiteSpace(dto.FormSchema))
            return NotFound(new { message = "Loại thiết bị chưa có biểu mẫu thông số kỹ thuật." });

        return Ok(new
        {
            id = dto.FormTemplateId,
            name = dto.FormTemplateName,
            formSchema = dto.FormSchema
        });
    }

    private async Task<List<long>?> GetAllowedUnitIdsAsync()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "SUPER_ADMIN");
        if (isAdmin)
        {
            return null;
        }

        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        if (!string.IsNullOrEmpty(unitIdClaim) && long.TryParse(unitIdClaim, out var userUnitId))
        {
            var allowedUnits = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(userUnitId);
            return allowedUnits.Select(u => u.Id).ToList();
        }

        var fallbackUnitIds = GetAuthorizedUnitIds();
        if (fallbackUnitIds != null && fallbackUnitIds.Any())
        {
            var list = new List<long>();
            foreach (var fId in fallbackUnitIds)
            {
                var units = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(fId);
                list.AddRange(units.Select(u => u.Id));
            }
            return list.Distinct().ToList();
        }

        return new List<long> { -1 };
    }

    private List<long>? GetAuthorizedUnitIds()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "SUPER_ADMIN");
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

    public class UnitRoleDto
    {
        public long UnitId { get; set; }
        public long RoleId { get; set; }
        public string RoleCode { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }
}
