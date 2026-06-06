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
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IMemoryCache _cache;

    public UsersController(IUserRepository userRepository, IPermissionRepository permissionRepository, IMemoryCache cache)
    {
        _userRepository = userRepository;
        _permissionRepository = permissionRepository;
        _cache = cache;
    }

    [HttpGet("lookup")]
    [Authorize]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetLookup()
    {
        var users = await _userRepository.GetAllAsync();
        var result = users.Select(u => new { u.Id, u.Username, u.FullName });
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _userRepository.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var result = await _userRepository.GetByIdAsync(id);
        if (result == null) return NotFound(new { message = "Không tìm thấy người dùng này." });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] User user)
    {
        if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.FullName))
        {
            return BadRequest(new { message = "Tên đăng nhập và Họ tên là bắt buộc." });
        }
        if (!user.OrganizationUnitId.HasValue || user.OrganizationUnitId.Value <= 0)
        {
            return BadRequest(new { message = "Người dùng phải thuộc một đơn vị thành viên hợp lệ." });
        }
        
        // Hash a default password or empty password if not supplied                
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("DefaultUserPassword_123!");
        user.IsActive = true;
        
        var newId = await _userRepository.CreateAsync(user);
        user.Id = newId;
        return CreatedAtAction(nameof(GetById), new { id = newId }, user);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] User user)
    {
        if (id != user.Id) return BadRequest(new { message = "ID không trùng khớp." });
        if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.FullName))
        {
            return BadRequest(new { message = "Tên đăng nhập và Họ tên là bắt buộc." });
        }
        if (!user.OrganizationUnitId.HasValue || user.OrganizationUnitId.Value <= 0)
        {
            return BadRequest(new { message = "Người dùng phải thuộc một đơn vị thành viên hợp lệ." });
        }

        var existing = await _userRepository.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = "Không tìm thấy người dùng cần chỉnh sửa." });
        
        user.PasswordHash = existing.PasswordHash; // Keep existing hash
        await _userRepository.UpdateFullAsync(user);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _userRepository.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = "Không tìm thấy người dùng cần xóa." });
        
        await _userRepository.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("{userId}/permissions")]
    public async Task<IActionResult> GetPermissions(string userId)
    {
        var result = await _permissionRepository.GetPermissionsByUserIdAsync(userId);
        return Ok(result);
    }

    [HttpPost("{userId}/permissions")]
    public async Task<IActionResult> SavePermissions(string userId, [FromBody] List<string> permissionIds)
    {
        if (permissionIds == null) return BadRequest(new { message = "Danh sách quyền không hợp lệ." });
        
        var success = await _permissionRepository.AssignPermissionsToUserAsync(userId, permissionIds);
        if (!success) return BadRequest(new { message = "Không thể gán quyền cho người dùng." });

        // Xóa cache quyền của người dùng để thay đổi có hiệu lực ngay lập tức
        _cache.Remove($"UserPerms_{userId}");

        return Ok(new { message = "Gán quyền trực tiếp cho người dùng thành công!" });
     }

    [HttpGet("{userId}/roles")]
    public async Task<IActionResult> GetRoles(string userId)
    {
        var result = await _userRepository.GetDirectRoleIdsByUserIdAsync(userId);
        return Ok(result);
    }

    [HttpPost("{userId}/roles")]
    public async Task<IActionResult> AssignRoles(string userId, [FromBody] List<long> roleIds)
    {
        if (roleIds == null) return BadRequest(new { message = "Danh sách vai trò không hợp lệ." });
        
        var success = await _userRepository.AssignRolesToUserAsync(userId, roleIds);
        if (!success) return BadRequest(new { message = "Không thể gán vai trò cho người dùng." });

        // Xóa cache quyền của người dùng để thay đổi có hiệu lực ngay lập tức
        _cache.Remove($"UserPerms_{userId}");

        return Ok(new { message = "Gán vai trò trực tiếp cho người dùng thành công!" });
    }
}
