using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using EvnHanoi.IdentityService.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/roles")]
public class RolesController : ControllerBase
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IMemoryCache _cache;

    public RolesController(
        IRoleRepository roleRepository, 
        IPermissionRepository permissionRepository,
        IMemoryCache cache)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _cache = cache;
    }

    [HttpGet("lookup")]
    [Authorize]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetLookup()
    {
        var cacheKey = "RolesLookup";
        if (!_cache.TryGetValue(cacheKey, out IEnumerable<object>? result))
        {
            var roles = await _roleRepository.GetAllAsync();
            result = roles.Select(r => new { r.Id, r.Code, r.Name }).ToList();
            var cacheOptions = new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            _cache.Set(cacheKey, result, cacheOptions);
        }
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null)
    {
        var (items, totalCount) = await _roleRepository.GetPagedAsync(page, pageSize, keyword);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _roleRepository.GetByIdAsync(id);
        if (result == null) return NotFound(new { message = "Không tìm thấy vai trò này." });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Role role)
    {
        if (string.IsNullOrWhiteSpace(role.Code) || string.IsNullOrWhiteSpace(role.Name))
        {
            return BadRequest(new { message = "Mã và Tên vai trò là bắt buộc." });
        }
        var newId = await _roleRepository.CreateAsync(role);
        role.Id = newId;

        // Evict cache
        _cache.Remove("RolesLookup");

        return CreatedAtAction(nameof(GetById), new { id = newId }, role);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] Role role)
    {
        if (id != role.Id) return BadRequest(new { message = "ID không trùng khớp." });
        if (string.IsNullOrWhiteSpace(role.Code) || string.IsNullOrWhiteSpace(role.Name))
        {
            return BadRequest(new { message = "Mã và Tên vai trò là bắt buộc." });
        }

        var success = await _roleRepository.UpdateAsync(role);
        if (!success) return NotFound(new { message = "Không tìm thấy vai trò cần chỉnh sửa." });

        // Evict cache
        _cache.Remove("RolesLookup");

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var success = await _roleRepository.DeleteAsync(id);
        if (!success) return NotFound(new { message = "Không tìm thấy vai trò cần xóa." });

        // Evict cache
        _cache.Remove("RolesLookup");

        return NoContent();
    }

    // Lấy danh sách mã quyền đã gán cho vai trò
    [HttpGet("{id}/permissions")]
    public async Task<IActionResult> GetPermissions(long id)
    {
        var permissions = await _roleRepository.GetPermissionsByRoleIdAsync(id);
        return Ok(permissions);
    }

    // Gán quyền cho vai trò
    [HttpPost("{id}/permissions")]
    public async Task<IActionResult> AssignPermissions(long id, [FromBody] List<string> permissionCodes)
    {
        if (permissionCodes == null) return BadRequest(new { message = "Danh sách quyền không hợp lệ." });
        var success = await _roleRepository.AssignPermissionsToRoleAsync(id, permissionCodes);
        if (!success) return BadRequest(new { message = "Gán quyền không thành công." });
        return Ok(new { message = "Gán quyền cho vai trò thành công." });
    }

    // Lấy toàn bộ danh sách quyền động trong hệ thống từ Repository của Permission
    [HttpGet("permissions/all")]
    public async Task<IActionResult> GetAllPermissions()
    {
        var permissions = await _permissionRepository.GetAllPermissionsAsync();
        var result = permissions
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Code,
                p.Name,
                p.Description
            });
        return Ok(result);
    }
}
