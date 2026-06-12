using System;
using System.Linq;
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
        var cacheKey = "OrganizationUnitsLookup";
        if (!_cache.TryGetValue(cacheKey, out IEnumerable<object>? result))
        {
            var units = await _unitRepository.GetAllAsync();
            result = units.Select(u => new { u.Id, u.Code, u.Name, u.ParentId }).ToList();
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            _cache.Set(cacheKey, result, cacheOptions);
        }
        return Ok(result);
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
        if (string.IsNullOrWhiteSpace(unit.Code) || string.IsNullOrWhiteSpace(unit.Name))
        {
            return BadRequest(new { message = "Mã và Tên đơn vị là bắt buộc." });
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
        if (string.IsNullOrWhiteSpace(unit.Code) || string.IsNullOrWhiteSpace(unit.Name))
        {
            return BadRequest(new { message = "Mã và Tên đơn vị là bắt buộc." });
        }

        var success = await _unitRepository.UpdateAsync(unit);
        if (!success) return NotFound(new { message = "Không tìm thấy đơn vị cần chỉnh sửa." });

        _cache.Remove("OrganizationUnitsLookup");

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var success = await _unitRepository.DeleteAsync(id);
        if (!success) return NotFound(new { message = "Không tìm thấy đơn vị cần xóa." });

        _cache.Remove("OrganizationUnitsLookup");

        return NoContent();
    }
}
