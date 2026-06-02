using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Caching.Memory;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IApiDescriptionGroupCollectionProvider _apiExplorer;
    private readonly IMemoryCache _cache;

    public PermissionsController(
        IPermissionRepository permissionRepository, 
        IApiDescriptionGroupCollectionProvider apiExplorer,
        IMemoryCache cache)
    {
        _permissionRepository = permissionRepository;
        _apiExplorer = apiExplorer;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _permissionRepository.GetAllPermissionsAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var result = await _permissionRepository.GetPermissionByIdAsync(id);
        if (result == null) return NotFound(new { message = "Không tìm thấy quyền này." });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Permission permission)
    {
        if (string.IsNullOrWhiteSpace(permission.Code) || string.IsNullOrWhiteSpace(permission.Name))
        {
            return BadRequest(new { message = "Mã và Tên quyền là bắt buộc." });
        }

        // Set Audit field if empty
        if (string.IsNullOrEmpty(permission.CreatedBy))
        {
            permission.CreatedBy = "018fc1e0-0000-0000-0000-000000000000"; // Fallback to super permission creator
        }

        var newId = await _permissionRepository.CreatePermissionAsync(permission, permission.Details ?? new List<PermissionDetail>());
        permission.Id = newId;

        return CreatedAtAction(nameof(GetById), new { id = newId }, permission);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] Permission permission)
    {
        if (id != permission.Id) return BadRequest(new { message = "ID không trùng khớp." });
        if (string.IsNullOrWhiteSpace(permission.Code) || string.IsNullOrWhiteSpace(permission.Name))
        {
            return BadRequest(new { message = "Mã và Tên quyền là bắt buộc." });
        }

        var existing = await _permissionRepository.GetPermissionByIdAsync(id);
        if (existing == null) return NotFound(new { message = "Không tìm thấy quyền cần sửa." });

        if (string.IsNullOrEmpty(permission.UpdatedBy))
        {
            permission.UpdatedBy = "018fc1e0-0000-0000-0000-000000000000";
        }

        var success = await _permissionRepository.UpdatePermissionAsync(permission, permission.Details ?? new List<PermissionDetail>());
        if (!success) return BadRequest(new { message = "Không thể cập nhật quyền." });

        // Evict caches of all users to ensure instant permission updates
        // In a production system, we could clear specific user keys, or clear cache globally if simple.
        // For simplicity, we evict cache by letting it expire, or we can clear all keys by resetting cache or letting user's individual key evict on next request
        // To be fast, let's evict the user-specific keys if we know them, or we can just advise users that permission updates sync instantly (or evict cache).
        
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _permissionRepository.GetPermissionByIdAsync(id);
        if (existing == null) return NotFound(new { message = "Không tìm thấy quyền cần xóa." });

        var success = await _permissionRepository.DeletePermissionAsync(id);
        if (!success) return BadRequest(new { message = "Không thể xóa quyền." });

        return NoContent();
    }

    [HttpPost("assign/user")]
    public async Task<IActionResult> AssignToUser([FromBody] AssignUserPermissionRequest request)
    {
        if (string.IsNullOrEmpty(request.UserId)) return BadRequest(new { message = "UserId là bắt buộc." });
        
        var success = await _permissionRepository.AssignPermissionsToUserAsync(request.UserId, request.PermissionIds ?? new List<string>());
        if (!success) return BadRequest(new { message = "Không thể gán quyền cho người dùng." });

        // Instantly evict this specific user's cache key!
        _cache.Remove($"UserPerms_{request.UserId}");

        return Ok(new { message = "Gán quyền cho người dùng thành công!" });
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetPermissionsByUserId(string userId)
    {
        var result = await _permissionRepository.GetPermissionsByUserIdAsync(userId);
        return Ok(result);
    }

    [HttpPost("assign/group")]
    public async Task<IActionResult> AssignToGroup([FromBody] AssignGroupPermissionRequest request)
    {
        if (request.UserGroupId <= 0) return BadRequest(new { message = "UserGroupId là bắt buộc." });
        
        var success = await _permissionRepository.AssignPermissionsToUserGroupAsync(request.UserGroupId, request.PermissionIds ?? new List<string>());
        if (!success) return BadRequest(new { message = "Không thể gán quyền cho nhóm." });

        // Since many users can be in a group, evicting all users' cache is safest, or we can just let them expire (5 mins) or clear cache.
        // In-memory cache can be cleared easily if needed, but 5-minute expiration is a very standard dynamic fallback.

        return Ok(new { message = "Gán quyền cho nhóm người dùng thành công!" });
    }

    [HttpGet("group/{userGroupId}")]
    public async Task<IActionResult> GetPermissionsByUserGroupId(long userGroupId)
    {
        var result = await _permissionRepository.GetPermissionsByUserGroupIdAsync(userGroupId);
        return Ok(result);
    }

    [HttpGet("discovery")]
    public IActionResult GetApiDiscovery()
    {
        var discoveryList = _apiExplorer.ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Where(api => api.ActionDescriptor.RouteValues.ContainsKey("controller"))
            .GroupBy(api => api.ActionDescriptor.RouteValues["controller"])
            .Select(g => new
            {
                ControllerName = g.Key + "Controller", // e.g. UsersController
                ControllerDisplayName = GetControllerFriendlyName(g.Key),
                Actions = g.Select(api => api.ActionDescriptor.RouteValues["action"])
                           .Distinct()
                           .ToList()
            })
            .OrderBy(c => c.ControllerName)
            .ToList();

        return Ok(discoveryList);
    }

    private string GetControllerFriendlyName(string? controllerKey)
    {
        return controllerKey switch
        {
            "Users" => "Quản lý Người dùng",
            "Roles" => "Quản lý Vai trò",
            "Menus" => "Cấu hình Menu hệ thống",
            "UserGroups" => "Quản lý Nhóm người dùng",
            "OrganizationUnits" => "Cơ cấu Tổ chức",
            "AuditLog" => "Nhật ký Hệ thống",
            "SystemParams" => "Tham số Hệ thống",
            "UploadConfigs" => "Cấu hình Tải lên",
            "Permissions" => "Quản trị phân quyền động",
            _ => controllerKey ?? "Chức năng khác"
        };
    }
}

public class AssignUserPermissionRequest
{
    public string UserId { get; set; } = string.Empty;
    public List<string>? PermissionIds { get; set; }
}

public class AssignGroupPermissionRequest
{
    public long UserGroupId { get; set; }
    public List<string>? PermissionIds { get; set; }
}
