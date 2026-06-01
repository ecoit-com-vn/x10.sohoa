// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Controllers\UserUnitRolesController.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/user-unit-roles")]
public class UserUnitRolesController : ControllerBase
{
    private readonly IUserUnitRoleRepository _userUnitRoleRepository;

    public UserUnitRolesController(IUserUnitRoleRepository userUnitRoleRepository)
    {
        _userUnitRoleRepository = userUnitRoleRepository;
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUserId(long userId)
    {
        var result = await _userUnitRoleRepository.GetUnitRolesByUserIdAsync(userId);
        return Ok(result);
    }

    [HttpPost("user/{userId}")]
    public async Task<IActionResult> AssignUnitRoles(long userId, [FromBody] List<UserUnitRole> unitRoles)
    {
        if (unitRoles == null) return BadRequest(new { message = "Danh sách quyền theo đơn vị không hợp lệ." });
        var success = await _userUnitRoleRepository.AssignUnitRolesAsync(userId, unitRoles);
        if (!success) return BadRequest(new { message = "Thiết lập quyền theo đơn vị thất bại." });
        return Ok(new { message = "Thiết lập quyền theo đơn vị cho người dùng thành công." });
    }
}
