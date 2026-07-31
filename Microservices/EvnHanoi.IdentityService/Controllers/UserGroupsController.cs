// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Controllers\UserGroupsController.cs
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/user-groups")]
public class UserGroupsController : ControllerBase
{
    private readonly IUserGroupRepository _userGroupRepository;

    public UserGroupsController(IUserGroupRepository userGroupRepository)
    {
        _userGroupRepository = userGroupRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] bool? isActive = null)
    {
        var (items, totalCount) = await _userGroupRepository.GetPagedAsync(page, pageSize, keyword, isActive);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _userGroupRepository.GetByIdAsync(id);
        if (result == null) return NotFound(new { message = "Không tìm thấy nhóm người dùng này." });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserGroup group)
    {
        if (string.IsNullOrWhiteSpace(group.Name))
        {
            return BadRequest(new { message = "Tên nhóm người dùng là bắt buộc." });
        }
        group.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "SYSTEM";
        var newId = await _userGroupRepository.CreateAsync(group);
        group.Id = newId;
        return CreatedAtAction(nameof(GetById), new { id = newId }, group);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] UserGroup group)
    {
        if (id != group.Id) return BadRequest(new { message = "ID không trùng khớp." });
        if (string.IsNullOrWhiteSpace(group.Name))
        {
            return BadRequest(new { message = "Tên nhóm người dùng là bắt buộc." });
        }

        var success = await _userGroupRepository.UpdateAsync(group);
        if (!success) return NotFound(new { message = "Không tìm thấy nhóm người dùng cần chỉnh sửa." });
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var success = await _userGroupRepository.DeleteAsync(id);
        if (!success) return NotFound(new { message = "Không tìm thấy nhóm người dùng cần xóa." });
        return NoContent();
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetMembers(long id)
    {
        var members = await _userGroupRepository.GetMembersAsync(id);
        return Ok(members);
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AssignMembers(long id, [FromBody] List<string> userIds)
    {
        if (userIds == null) return BadRequest(new { message = "Danh sách thành viên không hợp lệ." });
        var success = await _userGroupRepository.AssignMembersAsync(id, userIds);
        if (!success) return BadRequest(new { message = "Gán thành viên không thành công." });
        return Ok(new { message = "Gán thành viên vào nhóm thành công." });
    }

    [HttpGet("{id}/roles")]
    public async Task<IActionResult> GetRoles(long id)
    {
        var roles = await _userGroupRepository.GetRolesAsync(id);
        return Ok(roles);
    }

    [HttpPost("{id}/roles")]
    public async Task<IActionResult> AssignRoles(long id, [FromBody] List<long> roleIds)
    {
        if (roleIds == null) return BadRequest(new { message = "Danh sách vai trò không hợp lệ." });
        var success = await _userGroupRepository.AssignRolesAsync(id, roleIds);
        if (!success) return BadRequest(new { message = "Gán vai trò không thành công." });
        return Ok(new { message = "Gán vai trò cho nhóm thành công." });
    }
}
