using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogRepository _catalogRepository;

    public CatalogController(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        long? unitId = null;
        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        if (long.TryParse(unitIdClaim, out var parsedUnitId))
        {
            unitId = parsedUnitId;
        }
        var result = await _catalogRepository.GetAllAsync(unitId);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _catalogRepository.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Catalog catalog)
    {
        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        if (catalog.UnitId.HasValue && long.TryParse(unitIdClaim, out var unitId))
        {
            catalog.UnitId = unitId;
        }
        else
        {
            catalog.UnitId = null;
        }

        var id = await _catalogRepository.CreateAsync(catalog);
        catalog.Id = id;
        return CreatedAtAction(nameof(GetById), new { id = id }, catalog);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] Catalog catalog)
    {
        if (id != catalog.Id) return BadRequest();

        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        if (catalog.UnitId.HasValue && long.TryParse(unitIdClaim, out var unitId))
        {
            catalog.UnitId = unitId;
        }
        else
        {
            catalog.UnitId = null;
        }

        var success = await _catalogRepository.UpdateAsync(catalog);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var success = await _catalogRepository.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }
}

