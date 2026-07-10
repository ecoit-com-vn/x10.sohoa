using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using EvnHanoi.IdentityService.Infrastructure.Security;
using EvnHanoi.Infrastructure.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionGroupRepository _permissionGroupRepository;
    private readonly IRbacScopeAuthorizationService _rbacScope;
    private readonly IMemoryCache _cache;

    public RolesController(
        IRoleRepository roleRepository,
        IPermissionGroupRepository permissionGroupRepository,
        IRbacScopeAuthorizationService rbacScope,
        IMemoryCache cache)
    {
        _roleRepository = roleRepository;
        _permissionGroupRepository = permissionGroupRepository;
        _rbacScope = rbacScope;
        _cache = cache;
    }

    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetLookup()
    {
        IEnumerable<Role> roles;
        if (_rbacScope.IsCentralAdmin(User))
        {
            roles = await _roleRepository.GetAllAsync();
        }
        else
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            if (string.IsNullOrEmpty(unitIdClaim) || !long.TryParse(unitIdClaim, out var unitId))
            {
                return Ok(Array.Empty<object>());
            }

            roles = await _roleRepository.GetAllAsync(RoleScopeTypes.UNIT.Id, unitId, includeDescendants: true);
        }

        return Ok(roles.Select(r => new { r.Id, r.Code, r.Name, r.ScopeTypeId, r.OrganizationUnitId }));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] int? scopeTypeId = null)
    {
        if (_rbacScope.IsCentralAdmin(User))
        {
            var (allItems, allTotal) = await _roleRepository.GetPagedAsync(page, pageSize, keyword, scopeTypeId);
            return Ok(new { items = allItems, totalCount = allTotal, page, pageSize });
        }

        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        if (string.IsNullOrEmpty(unitIdClaim) || !long.TryParse(unitIdClaim, out var unitId))
        {
            return Ok(new { items = Array.Empty<Role>(), totalCount = 0, page, pageSize });
        }

        var (items, totalCount) = await _roleRepository.GetPagedAsync(
            page, pageSize, keyword, RoleScopeTypes.UNIT.Id, unitId, includeDescendants: true);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _roleRepository.GetByIdAsync(id);
        if (result == null) return NotFound(new { message = "Không tìm thấy vai trò này." });

        try
        {
            await _rbacScope.EnsureCanManageRoleAsync(User, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Role role)
    {
        if (string.IsNullOrWhiteSpace(role.Code) || string.IsNullOrWhiteSpace(role.Name))
        {
            return BadRequest(new { message = "Mã và Tên vai trò là bắt buộc." });
        }

        if (_rbacScope.IsCentralAdmin(User))
        {
            if (role.ScopeTypeId == RoleScopeTypes.GLOBAL.Id)
            {
                role.OrganizationUnitId = null;
            }
        }
        else
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            if (string.IsNullOrEmpty(unitIdClaim) || !long.TryParse(unitIdClaim, out var unitId))
            {
                return StatusCode(403, new { message = "Không xác định được đơn vị quản lý." });
            }
            role.OrganizationUnitId = unitId;
            role.ScopeTypeId = RoleScopeTypes.UNIT.Id;
        }

        try
        {
            await _rbacScope.EnsureCanManageRoleAsync(User, role);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }

        var newId = await _roleRepository.CreateAsync(role);
        role.Id = newId;
        _cache.Remove("RolesLookup");
        HttpContext.SetAudit(newId.ToString(), role.Code, $"Tạo vai trò {role.Code}", "ROLE", AuditActions.Create);
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

        var existing = await _roleRepository.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = "Không tìm thấy vai trò cần chỉnh sửa." });

        role.ScopeTypeId = existing.ScopeTypeId;
        role.OrganizationUnitId = existing.OrganizationUnitId;

        try
        {
            await _rbacScope.EnsureCanManageRoleAsync(User, existing);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }

        var success = await _roleRepository.UpdateAsync(role);
        if (!success) return NotFound(new { message = "Không tìm thấy vai trò cần chỉnh sửa." });
        _cache.Remove("RolesLookup");
        HttpContext.SetAudit(id.ToString(), role.Code, $"Cập nhật vai trò {role.Code}", "ROLE", AuditActions.Update);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var role = await _roleRepository.GetByIdAsync(id);
        if (role == null) return NotFound(new { message = "Không tìm thấy vai trò cần xóa." });

        try
        {
            await _rbacScope.EnsureCanManageRoleAsync(User, role);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }

        var success = await _roleRepository.DeleteAsync(id);
        if (!success) return NotFound(new { message = "Không tìm thấy vai trò cần xóa." });
        _cache.Remove("RolesLookup");
        HttpContext.SetAudit(id.ToString(), role.Code, $"Xóa vai trò {role.Code}", "ROLE", AuditActions.Delete);
        return NoContent();
    }

    [HttpGet("{id}/permission-groups")]
    public async Task<IActionResult> GetPermissionGroups(long id)
    {
        var role = await _roleRepository.GetByIdAsync(id);
        if (role == null) return NotFound(new { message = "Không tìm thấy vai trò này." });

        try
        {
            await _rbacScope.EnsureCanManageRoleAsync(User, role);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }

        var groups = await _permissionGroupRepository.GetPermissionGroupsByRoleIdAsync(id);
        return Ok(groups.Select(g => new { g.Id, g.Code, g.Name, g.GroupType, g.OrganizationUnitId, g.OrganizationUnitName }));
    }

    [HttpPut("{id}/permission-groups")]
    public async Task<IActionResult> AssignPermissionGroups(long id, [FromBody] List<long> permissionGroupIds)
    {
        if (permissionGroupIds == null) return BadRequest(new { message = "Danh sách nhóm quyền không hợp lệ." });

        try
        {
            await _rbacScope.EnsureCanAssignPermissionGroupsToRoleAsync(User, id, permissionGroupIds);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }

        var success = await _roleRepository.AssignPermissionGroupsAsync(id, permissionGroupIds);
        if (!success) return BadRequest(new { message = "Phân bổ nhóm quyền không thành công." });
        HttpContext.SetAudit(id.ToString(), null, $"Phân bổ {permissionGroupIds.Count} nhóm quyền cho vai trò {id}", "ROLE", AuditActions.Manage);
        return Ok(new { message = "Phân bổ nhóm quyền cho vai trò thành công." });
    }

    [HttpGet("{id}/users")]
    public async Task<IActionResult> GetAssignedUsers(long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null)
    {
        var role = await _roleRepository.GetByIdAsync(id);
        if (role == null) return NotFound(new { message = "Không tìm thấy vai trò này." });

        try
        {
            await _rbacScope.EnsureCanManageRoleAsync(User, role);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }

        var (items, totalCount) = await _roleRepository.GetUsersByRoleIdPagedAsync(id, page, pageSize, keyword);
        return Ok(new { items, totalCount, page, pageSize });
    }
}
