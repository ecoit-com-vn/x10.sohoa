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
using System.Security.Claims;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/system-permission-groups")]
[Authorize]
public class SystemPermissionGroupsController : ControllerBase
{
    private readonly IPermissionGroupRepository _permissionGroupRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IRbacScopeAuthorizationService _rbacScope;
    private readonly IMemoryCache _cache;

    public SystemPermissionGroupsController(
        IPermissionGroupRepository permissionGroupRepository,
        IPermissionRepository permissionRepository,
        IRbacScopeAuthorizationService rbacScope,
        IMemoryCache cache)
    {
        _permissionGroupRepository = permissionGroupRepository;
        _permissionRepository = permissionRepository;
        _rbacScope = rbacScope;
        _cache = cache;
    }

    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetLookup()
    {
        if (!_rbacScope.IsCentralAdmin(User))
        {
            return Ok(Array.Empty<object>());
        }

        var groups = await _permissionGroupRepository.GetAllAsync(PermissionGroupTypes.System);
        return Ok(groups.Select(g => new { g.Id, g.Code, g.Name }));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
     [FromQuery] int page = 1,
     [FromQuery] int pageSize = 10,
     [FromQuery] string? keyword = null,
     [FromQuery] bool? isActive = null)
    {
        // Trim khoảng trắng đầu cuối trước khi tìm kiếm.
        var normalizedKeyword = string.IsNullOrWhiteSpace(keyword)
            ? null
            : keyword.Trim();

        var (items, totalCount, allCount) =
            await _permissionGroupRepository.GetPagedAsync(
                PermissionGroupTypes.System,
                page,
                pageSize,
                normalizedKeyword,
                isActive: isActive);

        return Ok(new
        {
            items,
            totalCount,
            allCount,
            page,
            pageSize
        });
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _permissionGroupRepository.GetByIdAsync(id, PermissionGroupTypes.System);
        if (result == null) return NotFound(new { message = "Không tìm thấy nhóm quyền hệ thống." });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PermissionGroup group)
    {
        try
        {
            await _rbacScope.EnsureCanManagePermissionGroupsAsync(User);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(group.Code) ||
            string.IsNullOrWhiteSpace(group.Name))
        {
            return BadRequest(new
            {
                message = "Mã và Tên nhóm quyền là bắt buộc."
            });
        }

        group.GroupType = PermissionGroupTypes.System;
        group.OrganizationUnitId = null;
        group.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "SYSTEM";
        var newId = await _permissionGroupRepository.CreateAsync(group);
        group.Id = newId;

        _cache.Remove("SystemPermissionGroupsLookup");

        HttpContext.SetAudit(
            newId.ToString(),
            group.Code,
            $"Tạo nhóm quyền HT {group.Code}",
            "PERMISSION_GROUP",
            AuditActions.Create);

        return CreatedAtAction(
            nameof(GetById),
            new { id = newId },
            group);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] PermissionGroup group)
    {
        try
        {
            await _rbacScope.EnsureCanManagePermissionGroupsAsync(User);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }

        if (id != group.Id) return BadRequest(new { message = "ID không trùng khớp." });
        group.GroupType = PermissionGroupTypes.System;
        group.OrganizationUnitId = null;
        var success = await _permissionGroupRepository.UpdateAsync(group);
        if (!success) return NotFound(new { message = "Không tìm thấy nhóm quyền cần chỉnh sửa." });
        _cache.Remove("SystemPermissionGroupsLookup");
        HttpContext.SetAudit(id.ToString(), group.Code, $"Cập nhật nhóm quyền HT {group.Code}", "PERMISSION_GROUP", AuditActions.Update);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            await _rbacScope.EnsureCanManagePermissionGroupsAsync(User);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }

        var group = await _permissionGroupRepository.GetByIdAsync(id, PermissionGroupTypes.System);
        var success = await _permissionGroupRepository.DeleteAsync(id, PermissionGroupTypes.System);
        if (!success) return NotFound(new { message = "Không tìm thấy nhóm quyền cần xóa." });
        _cache.Remove("SystemPermissionGroupsLookup");
        HttpContext.SetAudit(id.ToString(), group?.Code, $"Xóa nhóm quyền HT {group?.Code}", "PERMISSION_GROUP", AuditActions.Delete);
        return NoContent();
    }

    [HttpGet("{id}/permissions")]
    public async Task<IActionResult> GetPermissions(long id)
    {
        var permissions = await _permissionGroupRepository.GetPermissionCodesByGroupIdAsync(id);
        return Ok(permissions);
    }

    [HttpPost("{id}/permissions")]
    public async Task<IActionResult> AssignPermissions(long id, [FromBody] List<string> permissionCodes)
    {
        try
        {
            await _rbacScope.EnsureCanManagePermissionGroupsAsync(User);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }

        if (permissionCodes == null) return BadRequest(new { message = "Danh sách quyền không hợp lệ." });
        var success = await _permissionGroupRepository.AssignPermissionsToGroupAsync(id, permissionCodes);
        if (!success) return BadRequest(new { message = "Gán quyền không thành công." });
        HttpContext.SetAudit(id.ToString(), null, $"Gán {permissionCodes.Count} quyền cho nhóm HT {id}", "PERMISSION_GROUP", AuditActions.Manage);
        return Ok(new { message = "Gán quyền cho nhóm quyền thành công." });
    }

    [HttpGet("permissions/all")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetAllPermissions()
    {
        var permissions = await _permissionRepository.GetAllPermissionsAsync();
        var result = permissions
            .Where(p => p.IsActive)
            .Select(p => new { p.Code, p.Name, p.Description });
        return Ok(result);
    }
    /// <summary>
    /// Lấy username của người dùng đang đăng nhập từ JWT.
    /// Không fallback sang SYSTEM vì đây là thao tác trực tiếp của người dùng.
    /// </summary>
    private string CurrentUsername
    {
        get
        {
            var username =
                User.FindFirst("preferred_username")?.Value
                ?? User.FindFirst("username")?.Value
                ?? User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new UnauthorizedAccessException(
                    "Không xác định được người dùng hiện tại.");
            }

            // Chuẩn hóa username trước khi lưu vào PERMISSION_GROUP.CreatedBy.
            return username.Trim();
        }
    }
}
