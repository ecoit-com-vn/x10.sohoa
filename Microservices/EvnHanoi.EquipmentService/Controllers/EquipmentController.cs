using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class EquipmentController : ControllerBase
{
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IEquipmentTypeRepository _equipmentTypeRepository;
    private readonly IMessageProducer _messageProducer;

    public EquipmentController(
        IEquipmentRepository equipmentRepository, 
        IEquipmentTypeRepository equipmentTypeRepository,
        IMessageProducer messageProducer)
    {
        _equipmentRepository = equipmentRepository;
        _equipmentTypeRepository = equipmentTypeRepository;
        _messageProducer = messageProducer;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? code = null,
        [FromQuery] string? name = null,
        [FromQuery] long? unitId = null,
        [FromQuery] Guid? infrastructureId = null,
        [FromQuery] int? gridTypeId = null,
        [FromQuery] Guid? equipmentTypeId = null,
        [FromQuery] bool? isActive = null)
    {
        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null)
        {
            if (unitId.HasValue && !allowedUnitIds.Contains(unitId.Value))
            {
                return Ok(new { items = new List<EquipmentDto>(), totalCount = 0, page, pageSize });
            }
        }

        var (items, totalCount) = await _equipmentRepository.GetPagedAsync(
            page, 
            pageSize, 
            code, 
            name, 
            unitId, 
            infrastructureId, 
            gridTypeId, 
            equipmentTypeId, 
            isActive, 
            allowedUnitIds);

        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("check-code")]
    public async Task<IActionResult> CheckCode([FromQuery] string code, [FromQuery] Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Mã thiết bị không được để trống." });

        var existing = await _equipmentRepository.GetByCodeAsync(code);
        var exists = existing != null && (!excludeId.HasValue || existing.Id != excludeId.Value);
        return Ok(new { exists });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(id);
        if (dto == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value)))
        {
            return Forbid();
        }

        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EquipmentCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Mã và Tên thiết bị là bắt buộc." });

        if (dto.EquipmentTypeId == Guid.Empty)
            return BadRequest(new { message = "Loại thiết bị không hợp lệ." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null)
        {
            if (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value))
            {
                return BadRequest(new { message = "Bạn không có quyền quản lý dữ liệu của đơn vị được chọn." });
            }
        }

        var trimmedCode = dto.Code.Trim();
        var existingByCode = await _equipmentRepository.GetByCodeAsync(trimmedCode);
        if (existingByCode != null)
        {
            var duplicateMessage = $"Mã thiết bị '{trimmedCode}' đã tồn tại trong hệ thống.";
            return BadRequest(new { message = duplicateMessage, code = duplicateMessage });
        }

        var equipmentId = Guid.NewGuid();
        var equipment = new Equipment
        {
            Id = equipmentId,
            EquipmentTypeId = dto.EquipmentTypeId,
            Name = dto.Name.Trim(),
            Code = dto.Code.Trim(),
            SerialNumber = dto.SerialNumber?.Trim() ?? string.Empty,
            InfrastructureId = dto.InfrastructureId,
            CountryId = dto.CountryId,
            IsActive = dto.IsActive,
            UnitId = dto.UnitId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system"
        };

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var creatorGuid))
        {
            equipment.CreatorId = creatorGuid;
        }

        var result = await _equipmentRepository.CreateAsync(equipment);
        if (result)
        {
            try
            {
                var syncMessage = new
                {
                    Id = equipment.Id,
                    EquipmentTypeId = equipment.EquipmentTypeId,
                    Name = equipment.Name,
                    Code = equipment.Code,
                    SerialNumber = equipment.SerialNumber,
                    CreatedAt = equipment.CreatedAt,
                    CreatedBy = equipment.CreatedBy,
                    UnitId = equipment.UnitId
                };
                await _messageProducer.SendMessageAsync(syncMessage, "equipment_sync_queue");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to publish sync message for equipment creation.");
            }

            var createdDto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
            return CreatedAtAction(nameof(GetById), new { id = equipmentId }, createdDto);
        }

        return BadRequest(new { message = "Không thể tạo thiết bị mới." });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EquipmentUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Mã và Tên thiết bị là bắt buộc." });

        if (dto.EquipmentTypeId == Guid.Empty)
            return BadRequest(new { message = "Loại thiết bị không hợp lệ." });

        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null)
        {
            if (!existing.UnitId.HasValue || !allowedUnitIds.Contains(existing.UnitId.Value))
            {
                return Forbid();
            }
            if (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value))
            {
                return BadRequest(new { message = "Bạn không có quyền quản lý dữ liệu của đơn vị mới được chọn." });
            }
        }

        var trimmedCode = dto.Code.Trim();
        var existingByCode = await _equipmentRepository.GetByCodeAsync(trimmedCode);
        if (existingByCode != null && existingByCode.Id != id)
        {
            var duplicateMessage = $"Mã thiết bị '{trimmedCode}' đã được sử dụng bởi bản ghi khác.";
            return BadRequest(new { message = duplicateMessage, code = duplicateMessage });
        }

        existing.EquipmentTypeId = dto.EquipmentTypeId;
        existing.Name = dto.Name.Trim();
        existing.Code = dto.Code.Trim();
        existing.SerialNumber = dto.SerialNumber?.Trim() ?? string.Empty;
        existing.InfrastructureId = dto.InfrastructureId;
        existing.CountryId = dto.CountryId;
        existing.UnitId = dto.UnitId;
        existing.IsActive = dto.IsActive;
        existing.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        existing.ModifiedDate = DateTime.UtcNow;

        var result = await _equipmentRepository.UpdateAsync(existing);
        if (result)
        {
            try
            {
                var syncMessage = new
                {
                    Id = existing.Id,
                    EquipmentTypeId = existing.EquipmentTypeId,
                    Name = existing.Name,
                    Code = existing.Code,
                    SerialNumber = existing.SerialNumber,
                    CreatedAt = existing.CreatedAt,
                    CreatedBy = existing.CreatedBy,
                    UnitId = existing.UnitId
                };
                await _messageProducer.SendMessageAsync(syncMessage, "equipment_sync_queue");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to publish sync message for equipment update.");
            }

            return NoContent();
        }

        return BadRequest(new { message = "Không thể cập nhật thiết bị." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!existing.UnitId.HasValue || !allowedUnitIds.Contains(existing.UnitId.Value)))
        {
            return Forbid();
        }

        var result = await _equipmentRepository.DeleteAsync(id);
        if (result)
            return NoContent();

        return BadRequest(new { message = "Không thể xóa thiết bị." });
    }

    [HttpPost("{id}/lock")]
    public async Task<IActionResult> Lock(Guid id)
    {
        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!existing.UnitId.HasValue || !allowedUnitIds.Contains(existing.UnitId.Value)))
        {
            return Forbid();
        }

        existing.IsActive = false;
        existing.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        existing.ModifiedDate = DateTime.UtcNow;

        var result = await _equipmentRepository.UpdateAsync(existing);
        if (result)
            return Ok(new { message = "Đã khóa thiết bị thành công." });

        return BadRequest(new { message = "Không thể khóa thiết bị." });
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock(Guid id)
    {
        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!existing.UnitId.HasValue || !allowedUnitIds.Contains(existing.UnitId.Value)))
        {
            return Forbid();
        }

        existing.IsActive = true;
        existing.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        existing.ModifiedDate = DateTime.UtcNow;

        var result = await _equipmentRepository.UpdateAsync(existing);
        if (result)
            return Ok(new { message = "Đã mở khóa thiết bị thành công." });

        return BadRequest(new { message = "Không thể mở khóa thiết bị." });
    }

    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetLookup()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        long? startUnitId = null;
        if (!isAdmin)
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            if (!string.IsNullOrEmpty(unitIdClaim) && long.TryParse(unitIdClaim, out var userUnitId))
            {
                startUnitId = userUnitId;
            }
            else
            {
                var fallbackUnitIds = GetAuthorizedUnitIds();
                if (fallbackUnitIds != null && fallbackUnitIds.Any())
                {
                    startUnitId = fallbackUnitIds.First();
                }
            }
        }

        var allowedUnitIdsForInfra = await GetAllowedUnitIdsAsync();
        var organizationUnits = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(startUnitId);
        var infrastructures = await _equipmentRepository.GetInfrastructuresLookupAsync(allowedUnitIdsForInfra);
        var gridTypes = await _equipmentTypeRepository.GetGridTypesAsync();
        var equipmentTypes = await _equipmentRepository.GetEquipmentTypesLookupAsync();
        var countries = await _equipmentRepository.GetCountriesAsync();

        return Ok(new
        {
            organizationUnits,
            infrastructures,
            gridTypes,
            equipmentTypes,
            countries
        });
    }

    [HttpGet("get-organization-units")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetOrganizationUnits()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        long? startUnitId = null;
        if (!isAdmin)
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            if (!string.IsNullOrEmpty(unitIdClaim) && long.TryParse(unitIdClaim, out var userUnitId))
            {
                startUnitId = userUnitId;
            }
            else
            {
                var fallbackUnitIds = GetAuthorizedUnitIds();
                if (fallbackUnitIds != null && fallbackUnitIds.Any())
                {
                    startUnitId = fallbackUnitIds.First();
                }
            }
        }

        var data = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(startUnitId);
        return Ok(data);
    }

    [HttpGet("get-infrastructures")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetInfrastructures()
    {
        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        var data = await _equipmentRepository.GetInfrastructuresLookupAsync(allowedUnitIds);
        return Ok(data);
    }

    [HttpGet("get-grid-types")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetGridTypes()
    {
        var data = await _equipmentTypeRepository.GetGridTypesAsync();
        return Ok(data);
    }

    [HttpGet("get-equipment-types")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetEquipmentTypes()
    {
        var data = await _equipmentRepository.GetEquipmentTypesLookupAsync();
        return Ok(data);
    }

    [HttpGet("get-countries")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetCountries()
    {
        var data = await _equipmentRepository.GetCountriesAsync();
        return Ok(data);
    }

    private async Task<List<long>?> GetAllowedUnitIdsAsync()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
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

    public class UnitRoleDto
    {
        public long UnitId { get; set; }
        public long RoleId { get; set; }
        public string RoleCode { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }

    [HttpPut("{id}/form-values")]
    public async Task<IActionResult> UpdateFormValues(Guid id, [FromBody] UpdateFormValuesRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!existing.UnitId.HasValue || !allowedUnitIds.Contains(existing.UnitId.Value)))
        {
            return Forbid();
        }

        existing.FormValues = request.FormValues;
        existing.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        existing.ModifiedDate = DateTime.UtcNow;

        var result = await _equipmentRepository.UpdateAsync(existing);
        if (result)
        {
            try
            {
                var syncMessage = new
                {
                    Id = existing.Id,
                    EquipmentTypeId = existing.EquipmentTypeId,
                    Name = existing.Name,
                    Code = existing.Code,
                    SerialNumber = existing.SerialNumber,
                    CreatedAt = existing.CreatedAt,
                    CreatedBy = existing.CreatedBy,
                    UnitId = existing.UnitId
                };
                await _messageProducer.SendMessageAsync(syncMessage, "equipment_sync_queue");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to publish sync message for equipment form values update.");
            }

            return NoContent();
        }

        return BadRequest(new { message = "Không thể cập nhật thông số thiết bị." });
    }
}

public class UpdateFormValuesRequest
{
    public string FormValues { get; set; } = string.Empty;
}
