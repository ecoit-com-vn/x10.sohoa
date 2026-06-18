using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class EquipmentTypeController : ControllerBase
{
    private readonly IEquipmentTypeRepository _repository;

    public EquipmentTypeController(IEquipmentTypeRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? code = null,
        [FromQuery] string? name = null,
        [FromQuery] int? gridTypeId = null,
        [FromQuery] bool? isActive = null)
    {
        if (page.HasValue || pageSize.HasValue)
        {
            var p = page ?? 1;
            var ps = pageSize ?? 10;
            var (items, totalCount) = await _repository.GetPagedAsync(p, ps, code, name, gridTypeId, isActive);
            return Ok(new { items, totalCount, page = p, pageSize = ps });
        }
        else
        {
            // Fallback for dashboard and search
            var items = await _repository.GetAllAsync();
            return Ok(items);
        }
    }

    [HttpGet("grid-types/lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetGridTypesLookup()
    {
        var items = await _repository.GetGridTypesAsync();
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var type = await _repository.GetByIdAsync(id);
        if (type == null)
            return NotFound();

        return Ok(type);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EquipmentType type)
    {
        if (string.IsNullOrWhiteSpace(type.Code) || string.IsNullOrWhiteSpace(type.Name))
            return BadRequest(new { message = "Mã và Tên loại thiết bị là bắt buộc." });

        type.Id = Guid.NewGuid();
        
        // Read creator info from Claims
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var creatorGuid))
        {
            type.CreatorId = creatorGuid;
        }
        type.CreatedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        type.IsActive = true;
        type.IsDeleted = false;

        var result = await _repository.CreateAsync(type);
        if (result)
        {
            var createdDto = await _repository.GetByIdAsync(type.Id);
            return CreatedAtAction(nameof(GetById), new { id = type.Id }, createdDto);
        }

        return BadRequest("Failed to create equipment type.");
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EquipmentType type)
    {
        if (id != type.Id)
            return BadRequest(new { message = "ID không trùng khớp." });

        if (string.IsNullOrWhiteSpace(type.Code) || string.IsNullOrWhiteSpace(type.Name))
            return BadRequest(new { message = "Mã và Tên loại thiết bị là bắt buộc." });

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        type.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";

        var result = await _repository.UpdateAsync(type);
        if (result)
            return Ok(type);

        return BadRequest("Failed to update equipment type.");
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var result = await _repository.DeleteAsync(id);
        if (result)
            return NoContent();

        return BadRequest("Failed to delete equipment type.");
    }

    [HttpPost("{id}/lock")]
    public async Task<IActionResult> Lock(Guid id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var type = new EquipmentType
        {
            Id = existing.Id,
            Code = existing.Code,
            Name = existing.Name,
            Description = existing.Description,
            GridTypeId = existing.GridTypeId,
            SortOrder = existing.SortOrder,
            IsActive = false,
            ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system"
        };

        var result = await _repository.UpdateAsync(type);
        if (result)
            return Ok(new { message = "Khóa loại thiết bị thành công." });

        return BadRequest("Failed to lock equipment type.");
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock(Guid id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var type = new EquipmentType
        {
            Id = existing.Id,
            Code = existing.Code,
            Name = existing.Name,
            Description = existing.Description,
            GridTypeId = existing.GridTypeId,
            SortOrder = existing.SortOrder,
            IsActive = true,
            ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system"
        };

        var result = await _repository.UpdateAsync(type);
        if (result)
            return Ok(new { message = "Mở khóa loại thiết bị thành công." });

        return BadRequest("Failed to unlock equipment type.");
    }

    [HttpGet("{id}/attributes")]
    public async Task<IActionResult> GetAttributes(Guid id)
    {
        var attributes = await _repository.GetAttributeDefinitionsAsync(id);
        return Ok(attributes);
    }

    [HttpPost("{id}/attributes")]
    public async Task<IActionResult> AddAttribute(Guid id, [FromBody] AttributeDefinition attribute)
    {
        attribute.Id = Guid.NewGuid();
        attribute.EquipmentTypeId = id;
        
        var result = await _repository.AddAttributeDefinitionAsync(attribute);
        if (result)
            return Ok(attribute);

        return BadRequest("Failed to add attribute definition.");
    }
}
