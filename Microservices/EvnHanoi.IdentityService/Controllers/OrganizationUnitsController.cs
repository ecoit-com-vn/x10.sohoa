using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.IdentityService.Controllers;

[ApiController]
[Route("api/v1/organization-units")]
public class OrganizationUnitsController : ControllerBase
{
    private readonly IOrganizationUnitRepository _unitRepository;

    public OrganizationUnitsController(IOrganizationUnitRepository unitRepository)
    {
        _unitRepository = unitRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _unitRepository.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _unitRepository.GetByIdAsync(id);
        if (result == null) return NotFound(new { message = "Không tìm thấy đơn vị này." });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrganizationUnit unit)
    {
        if (string.IsNullOrWhiteSpace(unit.Code) || string.IsNullOrWhiteSpace(unit.Name))
        {
            return BadRequest(new { message = "Mã và Tên đơn vị là bắt buộc." });
        }
        var newId = await _unitRepository.CreateAsync(unit);
        unit.Id = newId;
        return CreatedAtAction(nameof(GetById), new { id = newId }, unit);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] OrganizationUnit unit)
    {
        if (id != unit.Id) return BadRequest(new { message = "ID không trùng khớp." });
        if (string.IsNullOrWhiteSpace(unit.Code) || string.IsNullOrWhiteSpace(unit.Name))
        {
            return BadRequest(new { message = "Mã và Tên đơn vị là bắt buộc." });
        }

        var success = await _unitRepository.UpdateAsync(unit);
        if (!success) return NotFound(new { message = "Không tìm thấy đơn vị cần chỉnh sửa." });
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var success = await _unitRepository.DeleteAsync(id);
        if (!success) return NotFound(new { message = "Không tìm thấy đơn vị cần xóa." });
        return NoContent();
    }
}
