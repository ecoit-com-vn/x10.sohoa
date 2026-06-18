using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.Infrastructure.Database;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/dossier-sets")]
public class DossierSetController : ControllerBase
{
    private readonly IDossierSetRepository _repo;

    public DossierSetController(IDossierSetRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.Identity?.Name ?? "system";

    [HttpGet]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetAll([FromQuery] long? unitId)
    {
        var items = await _repo.GetAllAsync(unitId);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _repo.GetByIdAsync(id);
        if (item == null) return NotFound(new { message = $"Không tìm thấy bộ hồ sơ với ID = {id}" });
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DossierSetCreateDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        var entity = new DossierSet
        {
            Code = dto.Code,
            Name = dto.Name,
            UnitId = dto.UnitId,
            CreatedBy = UserId,
            CreatedDate = DateTime.UtcNow
        };

        var newId = await _repo.CreateAsync(entity);
        return CreatedAtAction(nameof(GetById), new { id = newId }, new { id = newId });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DossierSetUpdateDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        var existing = await _repo.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = $"Không tìm thấy bộ hồ sơ với ID = {id}" });

        existing.Code = dto.Code;
        existing.Name = dto.Name;
        existing.UnitId = dto.UnitId;
        existing.ModifiedBy = UserId;
        existing.ModifiedDate = DateTime.UtcNow;

        var result = await _repo.UpdateAsync(existing);
        return result ? NoContent() : BadRequest(new { message = "Cập nhật thất bại." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var existing = await _repo.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = $"Không tìm thấy bộ hồ sơ với ID = {id}" });

        var result = await _repo.SoftDeleteAsync(id, UserId);
        return result ? NoContent() : BadRequest(new { message = "Xóa thất bại." });
    }
}
