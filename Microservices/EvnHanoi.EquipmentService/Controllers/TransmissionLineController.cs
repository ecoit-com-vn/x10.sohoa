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
[Route("api/catalog/transmission-line")]
public class TransmissionLineController : ControllerBase
{
    private readonly IInfrastructureRepository _infrastructureRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private const int INFRA_TYPE_ID = 2; // 2 = Transmission Line (Đường dây)

    public TransmissionLineController(IInfrastructureRepository infrastructureRepository, IEquipmentRepository equipmentRepository)
    {
        _infrastructureRepository = infrastructureRepository;
        _equipmentRepository = equipmentRepository;
    }

    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> Lookup([FromQuery] string? keyword = null)
    {
        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        var (items, _) = await _infrastructureRepository.GetPagedAsync(1, 1000, INFRA_TYPE_ID, keyword, 1, allowedUnitIds);
        return Ok(items);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] int? status = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        var (items, totalCount) = await _infrastructureRepository.GetPagedAsync(page, pageSize, INFRA_TYPE_ID, keyword, status, allowedUnitIds);
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Infrastructure infrastructure)
    {
        if (string.IsNullOrWhiteSpace(infrastructure.Code) || string.IsNullOrWhiteSpace(infrastructure.Name))
            return BadRequest(new { message = "Mã và Tên đường dây là bắt buộc." });
        if (infrastructure.GridTypeId == null || infrastructure.GridTypeId <= 0)
            return BadRequest(new { message = "Loại lưới điện là bắt buộc." });

        // Force Transmission Line type
        infrastructure.InfraTypeId = INFRA_TYPE_ID;

        // Verify code uniqueness
        var existing = await _infrastructureRepository.GetByCodeAsync(infrastructure.Code);
        if (existing != null)
            return BadRequest(new { message = $"Mã '{infrastructure.Code}' đã tồn tại trong hệ thống." });

        infrastructure.CreatedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        infrastructure.CreatedDate = DateTime.UtcNow;
        infrastructure.IsDeleted = false;

        var id = await _infrastructureRepository.CreateAsync(infrastructure);
        infrastructure.Id = id;

        return CreatedAtAction(nameof(GetById), new { id }, infrastructure);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Infrastructure infrastructure)
    {
        if (id != infrastructure.Id)
            return BadRequest(new { message = "ID không trùng khớp." });

        if (string.IsNullOrWhiteSpace(infrastructure.Code) || string.IsNullOrWhiteSpace(infrastructure.Name))
            return BadRequest(new { message = "Mã và Tên đường dây là bắt buộc." });
        if (infrastructure.GridTypeId == null || infrastructure.GridTypeId <= 0)
            return BadRequest(new { message = "Loại lưới điện là bắt buộc." });

        // Force Transmission Line type
        infrastructure.InfraTypeId = INFRA_TYPE_ID;

        var existing = await _infrastructureRepository.GetByCodeAsync(infrastructure.Code);
        if (existing != null && existing.Id != id)
            return BadRequest(new { message = $"Mã '{infrastructure.Code}' đã được sử dụng bởi bản ghi khác." });

        var record = await _infrastructureRepository.GetByIdAsync(id);
        if (record == null || record.InfraTypeId != INFRA_TYPE_ID)
            return NotFound();

        infrastructure.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        infrastructure.ModifiedDate = DateTime.UtcNow;

        var success = await _infrastructureRepository.UpdateAsync(infrastructure);
        if (!success)
            return StatusCode(500, new { message = "Không thể cập nhật thông tin đường dây." });

        return Ok(infrastructure);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var record = await _infrastructureRepository.GetByIdAsync(id);
        if (record == null || record.InfraTypeId != INFRA_TYPE_ID)
            return NotFound();

        var success = await _infrastructureRepository.DeleteAsync(id);
        if (!success)
            return StatusCode(500, new { message = "Không thể xóa đường dây." });

        return Ok(new { message = "Xóa đường dây thành công." });
    }

    [HttpPost("{id:guid}/lock")]
    public async Task<IActionResult> Lock(Guid id)
    {
        var record = await _infrastructureRepository.GetByIdAsync(id);
        if (record == null || record.InfraTypeId != INFRA_TYPE_ID)
            return NotFound();

        record.IsActive = false;
        record.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        record.ModifiedDate = DateTime.UtcNow;

        var success = await _infrastructureRepository.UpdateAsync(record);
        if (!success)
            return StatusCode(500, new { message = "Không thể khóa đường dây." });

        return Ok(new { message = "Đã khóa đường dây thành công." });
    }

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id)
    {
        var record = await _infrastructureRepository.GetByIdAsync(id);
        if (record == null || record.InfraTypeId != INFRA_TYPE_ID)
            return NotFound();

        record.IsActive = true;
        record.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        record.ModifiedDate = DateTime.UtcNow;

        var success = await _infrastructureRepository.UpdateAsync(record);
        if (!success)
            return StatusCode(500, new { message = "Không thể mở khóa đường dây." });

        return Ok(new { message = "Đã mở khóa đường dây thành công." });
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
