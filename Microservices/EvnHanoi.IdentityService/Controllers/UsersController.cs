using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public UsersController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
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
}
