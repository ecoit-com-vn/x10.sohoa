using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PhysicalStorageController : ControllerBase
{
    private readonly IPhysicalStorageRepository _repository;

    public PhysicalStorageController(IPhysicalStorageRepository repository)
    {
        _repository = repository;
    }


    // --- SHELF ---
    [HttpGet("shelves")]
    public async Task<IActionResult> GetShelves() => Ok(await _repository.GetAllShelvesAsync());

    [HttpGet("shelves/{id}")]
    public async Task<IActionResult> GetShelfById(long id)
    {
        var result = await _repository.GetShelfByIdAsync(id);
        return result != null ? Ok(result) : NotFound();
    }

    [HttpPost("shelves")]
    public async Task<IActionResult> CreateShelf([FromBody] PhysicalShelf shelf)
    {
        var id = await _repository.CreateShelfAsync(shelf);
        return CreatedAtAction(nameof(GetShelfById), new { id = id }, shelf);
    }

    [HttpPut("shelves/{id}")]
    public async Task<IActionResult> UpdateShelf(long id, [FromBody] PhysicalShelf shelf)
    {
        if (id != shelf.Id) return BadRequest();
        return await _repository.UpdateShelfAsync(shelf) ? NoContent() : NotFound();
    }

    [HttpDelete("shelves/{id}")]
    public async Task<IActionResult> DeleteShelf(long id) => 
        await _repository.DeleteShelfAsync(id) ? NoContent() : NotFound();


    // --- FLOOR ---
    [HttpGet("shelves/{shelfId}/floors")]
    public async Task<IActionResult> GetFloors(long shelfId) => Ok(await _repository.GetFloorsByShelfIdAsync(shelfId));

    [HttpGet("floors/{id}")]
    public async Task<IActionResult> GetFloorById(long id)
    {
        var result = await _repository.GetFloorByIdAsync(id);
        return result != null ? Ok(result) : NotFound();
    }

    [HttpPost("floors")]
    public async Task<IActionResult> CreateFloor([FromBody] PhysicalFloor floor)
    {
        var id = await _repository.CreateFloorAsync(floor);
        return CreatedAtAction(nameof(GetFloorById), new { id = id }, floor);
    }

    [HttpPut("floors/{id}")]
    public async Task<IActionResult> UpdateFloor(long id, [FromBody] PhysicalFloor floor)
    {
        if (id != floor.Id) return BadRequest();
        return await _repository.UpdateFloorAsync(floor) ? NoContent() : NotFound();
    }

    [HttpDelete("floors/{id}")]
    public async Task<IActionResult> DeleteFloor(long id) => 
        await _repository.DeleteFloorAsync(id) ? NoContent() : NotFound();


    // --- BOX ---
    [HttpGet("floors/{floorId}/boxes")]
    public async Task<IActionResult> GetBoxes(long floorId) => Ok(await _repository.GetBoxesByFloorIdAsync(floorId));

    [HttpGet("boxes/{id}")]
    public async Task<IActionResult> GetBoxById(long id)
    {
        var result = await _repository.GetBoxByIdAsync(id);
        return result != null ? Ok(result) : NotFound();
    }

    [HttpPost("boxes")]
    public async Task<IActionResult> CreateBox([FromBody] PhysicalBox box)
    {
        var id = await _repository.CreateBoxAsync(box);
        return CreatedAtAction(nameof(GetBoxById), new { id = id }, box);
    }

    [HttpPut("boxes/{id}")]
    public async Task<IActionResult> UpdateBox(long id, [FromBody] PhysicalBox box)
    {
        if (id != box.Id) return BadRequest();
        return await _repository.UpdateBoxAsync(box) ? NoContent() : NotFound();
    }

    [HttpDelete("boxes/{id}")]
    public async Task<IActionResult> DeleteBox(long id) => 
        await _repository.DeleteBoxAsync(id) ? NoContent() : NotFound();
}
