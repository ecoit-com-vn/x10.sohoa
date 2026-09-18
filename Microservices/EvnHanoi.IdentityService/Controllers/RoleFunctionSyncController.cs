using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.DTOs;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

/// <summary>
/// API cho hệ thống ngoài đồng bộ Vai trò/Chức năng — không dùng JWT người dùng (hệ thống gọi vào
/// không có tài khoản trong app), xác thực bằng Private Key tĩnh cấp qua ExternalApiKeysController
/// (tạo 1 key với KeyName = "ROLE_FUNCTION_SYNC"), giống cơ chế PMIS gateway ở EquipmentExternalController.
/// "Chức năng" ở đây lấy từ APP_MENU (đã có sẵn Url + SortOrder khớp yêu cầu); quan hệ Vai trò-Chức năng
/// suy ra qua ROLE_PERMISSION_GROUP → PERMISSION_GROUP_PERMISSION → PERMISSION.Code = APP_MENU.PermissionCode.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/sync")]
public class RoleFunctionSyncController : ControllerBase
{
    private const string KeyName = "ROLE_FUNCTION_SYNC";
    private const string PrivateKeyHeader = "X-Sync-Private-Key";

    private readonly IRoleRepository _roleRepository;
    private readonly IMenuRepository _menuRepository;
    private readonly IExternalApiKeyValidator _externalApiKeyValidator;

    public RoleFunctionSyncController(
        IRoleRepository roleRepository,
        IMenuRepository menuRepository,
        IExternalApiKeyValidator externalApiKeyValidator)
    {
        _roleRepository = roleRepository;
        _menuRepository = menuRepository;
        _externalApiKeyValidator = externalApiKeyValidator;
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles([FromHeader(Name = PrivateKeyHeader)] string? privateKey)
    {
        if (!await IsAuthorizedAsync(privateKey))
            return Unauthorized(new { success = false, message = "Private key không hợp lệ hoặc đã hết hạn." });

        var roles = await _roleRepository.GetAllAsync();
        var data = roles
            .Where(r => r.IsActive)
            .OrderBy(r => r.Id)
            .Select(r => new { id = r.Id, title = r.Name });

        return Ok(new { success = true, data });
    }

    [HttpGet("functions")]
    public async Task<IActionResult> GetFunctions([FromHeader(Name = PrivateKeyHeader)] string? privateKey)
    {
        if (!await IsAuthorizedAsync(privateKey))
            return Unauthorized(new { success = false, message = "Private key không hợp lệ hoặc đã hết hạn." });

        var menus = await _menuRepository.GetAllAsync();
        var data = menus
            .Where(m => m.IsActive)
            .OrderBy(m => m.SortOrder)
            .Select(m => new
            {
                id = m.Id,
                title = m.Name,
                group_id = m.ParentId ?? 0,
                url = m.Url,
                sort = m.SortOrder
            });

        return Ok(new { success = true, data });
    }

    [HttpGet("role-functions")]
    public async Task<IActionResult> GetRoleFunctions([FromHeader(Name = PrivateKeyHeader)] string? privateKey)
    {
        if (!await IsAuthorizedAsync(privateKey))
            return Unauthorized(new { success = false, message = "Private key không hợp lệ hoặc đã hết hạn." });

        var assignments = await _menuRepository.GetRoleFunctionAssignmentsAsync();
        var data = assignments.Select(a => new { role_id = a.RoleId, func_id = a.FuncId });

        return Ok(new { success = true, data });
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole(
        [FromHeader(Name = PrivateKeyHeader)] string? privateKey,
        [FromBody] SyncCreateRoleRequest request)
    {
        if (!await IsAuthorizedAsync(privateKey))
            return Unauthorized(new { success = false, message = "Private key không hợp lệ hoặc đã hết hạn." });

        var title = request?.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { success = false, message = "Tên vai trò là bắt buộc." });

        var role = new Role
        {
            Code = GenerateRoleCode(),
            Name = title,
            Description = string.Empty,
            ScopeTypeId = RoleScopeTypes.GLOBAL.Id,
            OrganizationUnitId = null,
            IsActive = true,
            CreatedBy = "ROLE_FUNCTION_SYNC"
        };

        var newId = await _roleRepository.CreateAsync(role);
        return Ok(new { success = true, id = newId });
    }

    [HttpPost("roles/update")]
    public async Task<IActionResult> UpdateRole(
        [FromHeader(Name = PrivateKeyHeader)] string? privateKey,
        [FromBody] SyncUpdateRoleRequest request)
    {
        if (!await IsAuthorizedAsync(privateKey))
            return Unauthorized(new { success = false, message = "Private key không hợp lệ hoặc đã hết hạn." });

        var title = request?.Title?.Trim();
        if (request == null || request.Id <= 0 || string.IsNullOrWhiteSpace(title))
            return BadRequest(new { success = false, message = "Id và Tên vai trò là bắt buộc." });

        var existing = await _roleRepository.GetByIdAsync(request.Id);
        if (existing == null)
            return NotFound(new { success = false, message = "Không tìm thấy vai trò cần chỉnh sửa." });

        existing.Name = title;
        var updated = await _roleRepository.UpdateAsync(existing);
        if (!updated)
            return NotFound(new { success = false, message = "Không tìm thấy vai trò cần chỉnh sửa." });

        return Ok(new { success = true });
    }

    [HttpPost("roles/delete")]
    public async Task<IActionResult> DeleteRole(
        [FromHeader(Name = PrivateKeyHeader)] string? privateKey,
        [FromBody] SyncDeleteRoleRequest request)
    {
        if (!await IsAuthorizedAsync(privateKey))
            return Unauthorized(new { success = false, message = "Private key không hợp lệ hoặc đã hết hạn." });

        if (request == null || request.Id <= 0)
            return BadRequest(new { success = false, message = "Id vai trò là bắt buộc." });

        var deleted = await _roleRepository.DeleteAsync(request.Id);
        if (!deleted)
            return NotFound(new { success = false, message = "Không tìm thấy vai trò cần xóa." });

        return Ok(new { success = true });
    }

    private async Task<bool> IsAuthorizedAsync(string? privateKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey))
            return false;

        var apiKeyId = await _externalApiKeyValidator.ValidateAsync(KeyName, ComputeSha256(privateKey));
        return apiKeyId is not null;
    }

    /// <summary>Code chỉ dùng nội bộ (không lộ ra hợp đồng API sync) — random để tránh trùng lặp khi
    /// nhiều vai trò được đặt tên giống/gần giống nhau qua API.</summary>
    private static string GenerateRoleCode() => $"SYNC_{Guid.NewGuid():N}".Substring(0, 20).ToUpperInvariant();

    private static string ComputeSha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
