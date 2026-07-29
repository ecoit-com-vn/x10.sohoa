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
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IMemoryCache _cache;
    private readonly IRbacScopeAuthorizationService _rbacScope;

    public UsersController(IUserRepository userRepository, IPermissionRepository permissionRepository, IMemoryCache cache, IRbacScopeAuthorizationService rbacScope)
    {
        _userRepository = userRepository;
        _permissionRepository = permissionRepository;
        _cache = cache;
        _rbacScope = rbacScope;
    }

    [HttpGet("lookup")]
    [Authorize]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetLookup([FromQuery] string? role)
    {
        if (!string.IsNullOrWhiteSpace(role))
        {
            var filteredResult = await _userRepository.GetUsersLookupAsync(role);
            return Ok(filteredResult);
        }

        var cacheKey = "UsersLookup";
        if (!_cache.TryGetValue(cacheKey, out IEnumerable<UserLookupDto>? result))
        {
            result = await _userRepository.GetUsersLookupAsync(null);
            var cacheOptions = new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            _cache.Set(cacheKey, result, cacheOptions);
        }
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] long? organizationUnitId = null, [FromQuery] bool? isActive = null)
    {
        if (_rbacScope.IsCentralAdmin(User))
        {
            var (allItems, allTotal) = await _userRepository.GetPagedAsync(page, pageSize, keyword, organizationUnitId, isActive, includeDescendants: false);
            return Ok(new { items = allItems, totalCount = allTotal, page, pageSize });
        }

        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        if (string.IsNullOrEmpty(unitIdClaim) || !long.TryParse(unitIdClaim, out var unitId))
        {
            return Ok(new { items = Array.Empty<User>(), totalCount = 0, page, pageSize });
        }

        var (items, totalCount) = await _userRepository.GetPagedAsync(page, pageSize, keyword, unitId, isActive, includeDescendants: true);
        return Ok(new { items, totalCount, page, pageSize });
    }

    /// <summary>
    /// Lấy danh sách người dùng đủ điều kiện xử lý bước tiếp theo của luồng.
    /// Dùng tại màn hình chuyển xử lý hồ sơ để lọc người nhận theo nhóm quyền và đơn vị.
    /// </summary>
    [HttpGet("eligible-assignees")]
    [Authorize]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetEligibleAssignees(
        [FromQuery] string? systemGroupIds = null,
        [FromQuery] string? unitGroupIds = null,
        [FromQuery] long? unitId = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Parse danh sách ID từ chuỗi CSV
        var systemIds = ParseLongList(systemGroupIds);
        var unitIds   = ParseLongList(unitGroupIds);

        if (systemIds.Count == 0 && unitIds.Count == 0)
            return Ok(Array.Empty<object>());

        var result = await _userRepository.GetEligibleAssigneesAsync(systemIds, unitIds, unitId, keyword, page, pageSize);
        return Ok(result);
    }

    private static List<long> ParseLongList(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new List<long>();
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Select(s => s.Trim())
                  .Where(s => long.TryParse(s, out _))
                  .Select(long.Parse)
                  .ToList();
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
        // Check duplicate username
        var existingUser = await _userRepository.GetUserByUsernameAsync(user.Username.Trim());
        if (existingUser != null)
        {
            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = new Dictionary<string, string> { { "username", "Tên đăng nhập đã tồn tại trong hệ thống." } }
            });
        }
        
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123!");
        user.IsActive = true;
        try
        {
            var newId = await _userRepository.CreateAsync(user);
            user.Id = newId;
            _cache.Remove("UsersLookup");
            HttpContext.SetAudit(newId, user.Username, $"Tạo người dùng {user.Username}", "USER", AuditActions.Create);

            return CreatedAtAction(nameof(GetById), new { id = newId }, user);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống không mong muốn." });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] User user)
    {
        if (id != user.Id)
        {
            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = new Dictionary<string, string> { { "id", "ID không trùng khớp." } }
            });
        }

        var existing = await _userRepository.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = "Không tìm thấy người dùng cần chỉnh sửa." });

        // Do not allow modifying the username
        if (!string.Equals(user.Username?.Trim(), existing.Username, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                statusCode = 400,
                message = "Dữ liệu đầu vào không hợp lệ.",
                errors = new Dictionary<string, string> { { "username", "Không được phép thay đổi tên đăng nhập." } }
            });
        }
        
        await _userRepository.UpdateFullAsync(user);
        _cache.Remove("UsersLookup");
        HttpContext.SetAudit(id, user.Username, $"Cập nhật người dùng {user.Username}", "USER", AuditActions.Update);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var existing = await _userRepository.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = "Không tìm thấy người dùng cần xóa." });
        
        await _userRepository.DeleteAsync(id);
        _cache.Remove("UsersLookup");
        HttpContext.SetAudit(id, existing.Username, $"Xóa người dùng {existing.Username}", "USER", AuditActions.Delete);

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
        HttpContext.SetAudit(userId, null, $"Gán quyền trực tiếp cho user {userId}", "USER", AuditActions.Manage);

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
        HttpContext.SetAudit(userId, null, $"Gán vai trò cho user {userId}", "USER", AuditActions.Manage);

        return Ok(new { message = "Gán vai trò trực tiếp cho người dùng thành công!" });
    }
}
