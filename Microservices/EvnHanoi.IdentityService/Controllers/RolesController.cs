using System;
using System.Collections.Generic;
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

    public RolesController(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
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

    // Lấy toàn bộ danh sách quyền trong hệ thống
    [HttpGet("permissions/all")]
    public IActionResult GetAllPermissions()
    {
        var systemPermissions = new List<object>
        {
            new { Code = "VIEW_DASHBOARD", Name = "Xem bảng điều khiển", Description = "Xem giao diện thống kê, tổng quan hệ thống." },
            new { Code = "USER_MANAGE", Name = "Quản lý người dùng", Description = "Thêm, sửa, khóa tài khoản người dùng." },
            new { Code = "ROLE_MANAGE", Name = "Quản lý nhóm quyền", Description = "Xem danh sách và thay đổi vai trò (Role)." },
            new { Code = "PERMISSION_MANAGE", Name = "Phân quyền vai trò", Description = "Gán quyền thao tác cụ thể cho từng vai trò." },
            new { Code = "SYSTEM_PARAM_MANAGE", Name = "Quản lý cấu hình tham số", Description = "Xem và sửa đổi các tham số cài đặt hệ thống." },
            new { Code = "ORGANIZATION_MANAGE", Name = "Cài đặt tổ chức", Description = "Quản lý sơ đồ phòng ban, đơn vị thành viên." },
            new { Code = "CATALOG_MANAGE", Name = "Quản lý danh mục đơn vị", Description = "Quản lý danh mục đơn vị tính và các danh mục khác." },
            new { Code = "MENU_MANAGE", Name = "Quản lý Menu", Description = "Xem, thêm, sửa, xóa cấu hình Menu động." },
            new { Code = "USER_GROUP_MANAGE", Name = "Quản lý nhóm người dùng", Description = "Xem, thêm, sửa, xóa các nhóm người dùng (User Group)." },
            new { Code = "UPLOAD_CONFIG_MANAGE", Name = "Cấu hình upload file", Description = "Xem và sửa đổi các quy định về định dạng, dung lượng file tải lên." },
            new { Code = "AUDIT_LOG_VIEW", Name = "Xem nhật ký hệ thống", Description = "Xem danh sách nhật ký thao tác bảo mật hệ thống." },
            new { Code = "AUDIT_LOG_DELETE", Name = "Xóa nhật ký hệ thống", Description = "Thực hiện xóa/dọn dẹp nhật ký hệ thống một cách an toàn." }
        };
        return Ok(systemPermissions);
    }
}
