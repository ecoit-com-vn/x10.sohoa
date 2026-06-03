using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/roles")]
public class RolesController : ControllerBase
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;

    public RolesController(IRoleRepository roleRepository, IPermissionRepository permissionRepository)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _roleRepository.GetAllAsync();
        return Ok(result);
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
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var success = await _roleRepository.DeleteAsync(id);
        if (!success) return NotFound(new { message = "Không tìm thấy vai trò cần xóa." });
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
