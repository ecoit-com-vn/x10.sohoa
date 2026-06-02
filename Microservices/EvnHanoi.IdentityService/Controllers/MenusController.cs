// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Controllers\MenusController.cs
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using EvnHanoi.IdentityService.Infrastructure.Security;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/menus")]
public class MenusController : ControllerBase
{
    private readonly IMenuRepository _menuRepository;

    public MenusController(IMenuRepository menuRepository)
    {
        _menuRepository = menuRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _menuRepository.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("sidebar")]
    [Authorize]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetSidebar()
    {
        var permissionClaims = User.FindAll("permission");
        var permissions = new List<string>();
        foreach (var claim in permissionClaims)
        {
            permissions.Add(claim.Value);
        }

        var result = await _menuRepository.GetMenusByUserPermissionsAsync(permissions);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _menuRepository.GetByIdAsync(id);
        if (result == null) return NotFound(new { message = "Không tìm thấy Menu này." });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Menu menu)
    {
        if (string.IsNullOrWhiteSpace(menu.Name))
        {
            return BadRequest(new { message = "Tên Menu là bắt buộc." });
        }
        var newId = await _menuRepository.CreateAsync(menu);
        menu.Id = newId;
        return CreatedAtAction(nameof(GetById), new { id = newId }, menu);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] Menu menu)
    {
        if (id != menu.Id) return BadRequest(new { message = "ID không trùng khớp." });
        if (string.IsNullOrWhiteSpace(menu.Name))
        {
            return BadRequest(new { message = "Tên Menu là bắt buộc." });
        }

        var success = await _menuRepository.UpdateAsync(menu);
        if (!success) return NotFound(new { message = "Không tìm thấy Menu cần chỉnh sửa." });
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var success = await _menuRepository.DeleteAsync(id);
        if (!success) return NotFound(new { message = "Không tìm thấy Menu cần xóa." });
        return NoContent();
    }
}
