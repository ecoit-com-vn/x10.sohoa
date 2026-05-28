using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EquipmentTypeController : ControllerBase
{
    private readonly IEquipmentTypeRepository _repository;

    public EquipmentTypeController(IEquipmentTypeRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var types = await _repository.GetAllAsync();
        return Ok(types);
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
        type.Id = Guid.NewGuid();
        var result = await _repository.CreateAsync(type);
        if (result)
            return CreatedAtAction(nameof(GetById), new { id = type.Id }, type);

        return BadRequest("Failed to create equipment type.");
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EquipmentType type)
    {
        type.Id = id;
        var result = await _repository.UpdateAsync(type);
        if (result)
            return NoContent();

        return BadRequest("Failed to update equipment type.");
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _repository.DeleteAsync(id);
        if (result)
            return NoContent();

        return BadRequest("Failed to delete equipment type.");
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
