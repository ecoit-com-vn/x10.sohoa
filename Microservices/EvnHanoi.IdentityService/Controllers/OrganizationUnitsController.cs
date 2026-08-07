using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using EvnHanoi.IdentityService.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/organization-units")]
public class OrganizationUnitsController : ControllerBase
{
    private readonly IOrganizationUnitRepository _unitRepository;
    private readonly IMemoryCache _cache;

    public OrganizationUnitsController(IOrganizationUnitRepository unitRepository, IMemoryCache cache)
    {
        _unitRepository = unitRepository;
        _cache = cache;
    }

    [HttpGet("lookup")]
    [Authorize]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetLookup()
    {
        var isAdmin = User.IsInRole("ADMIN") || 
                      User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN") || 
                      User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "SUPER_ADMIN");

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
                return Ok(new List<object>());
            }
        }

        var cacheKey = $"OrganizationUnitsLookup_{(startUnitId.HasValue ? startUnitId.Value.ToString() : "ALL")}";
        if (!_cache.TryGetValue(cacheKey, out IEnumerable<object>? result))
        {
            var units = await _unitRepository.GetOrganizationUnitsHierarchicalAsync(startUnitId);
            result = units.Select(u => new { u.Id, u.Code, u.Name, u.ParentId }).ToList();
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            _cache.Set(cacheKey, result, cacheOptions);
        }
        return Ok(result);
    }

    [HttpGet("lookup-all-active")]
    [Authorize]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetAllActiveLookup()
    {
        var units = await _unitRepository.GetAllAsync();
        return Ok(units
            .Where(unit => unit.IsActive && !unit.IsDeleted)
            .Select(unit => new { unit.Id, unit.Code, unit.Name, unit.ParentId })
            .ToList());
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _unitRepository.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _unitRepository.GetByIdAsync(id);
        if (result == null) return NotFound(new { message = "Không tìm thấy đơn vị này." });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrganizationUnit unit)
    {
        unit.Code = unit.Code?.Trim() ?? string.Empty;
        unit.Name = unit.Name?.Trim() ?? string.Empty;
        unit.Description = unit.Description?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(unit.Code) || string.IsNullOrWhiteSpace(unit.Name))
        {
            return BadRequest(new { message = "Mã và Tên đơn vị là bắt buộc." });
        }
        if (!unit.SortOrder.HasValue || unit.SortOrder.Value < 1)
        {
            return BadRequest(new { message = "Thứ tự sắp xếp phải là số nguyên lớn hơn hoặc bằng 1." });
        }
        var newId = await _unitRepository.CreateAsync(unit);
        unit.Id = newId;

        _cache.Remove("OrganizationUnitsLookup");

        return CreatedAtAction(nameof(GetById), new { id = newId }, unit);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] OrganizationUnit unit)
    {
        if (id != unit.Id) return BadRequest(new { message = "ID không trùng khớp." });
        unit.Code = unit.Code?.Trim() ?? string.Empty;
        unit.Name = unit.Name?.Trim() ?? string.Empty;
        unit.Description = unit.Description?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(unit.Code) || string.IsNullOrWhiteSpace(unit.Name))
        {
            return BadRequest(new { message = "Mã và Tên đơn vị là bắt buộc." });
        }
        if (!unit.SortOrder.HasValue || unit.SortOrder.Value < 1)
        {
            return BadRequest(new { message = "Thứ tự sắp xếp phải là số nguyên lớn hơn hoặc bằng 1." });
        }

        var success = await _unitRepository.UpdateAsync(unit);
        if (!success) return NotFound(new { message = "Không tìm thấy đơn vị cần chỉnh sửa." });

        _cache.Remove("OrganizationUnitsLookup");

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var unit = await _unitRepository.GetByIdAsync(id);
        if (unit == null) return NotFound(new { message = "Không tìm thấy đơn vị cần xóa." });

        if (await _unitRepository.HasActiveChildrenAsync(id))
            return BadRequest(new { message = "Không thể xóa đơn vị này vì còn đơn vị trực thuộc chưa được xóa." });

        if (await _unitRepository.HasActiveUsersAsync(id))
            return BadRequest(new { message = "Không thể xóa đơn vị này vì còn tài khoản người dùng chưa được xóa." });

        if (await _unitRepository.HasActiveFoldersAsync(id))
            return BadRequest(new { message = "Không thể xóa đơn vị này vì còn thư mục tài liệu chưa được xóa." });

        if (await _unitRepository.HasActiveInfrastructureAsync(id))
            return BadRequest(new { message = "Không thể xóa đơn vị này vì còn hạ tầng (trạm/đường dây) chưa được xóa." });

        var success = await _unitRepository.DeleteAsync(id);
        if (!success) return NotFound(new { message = "Không tìm thấy đơn vị cần xóa." });

        _cache.Remove("OrganizationUnitsLookup");

        return NoContent();
    }
}
