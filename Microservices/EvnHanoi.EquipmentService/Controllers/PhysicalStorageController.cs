using System.Security.Claims;
using System.Text.Json;
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
    /// <summary>
    /// Danh sách kệ theo đơn vị hiện tại (query unitId hoặc phạm vi JWT).
    /// </summary>
    [HttpGet("shelves")]
    public async Task<IActionResult> GetShelves([FromQuery] long? unitId = null)
    {
        var scope = await ResolveUnitScopeAsync(unitId);
        if (scope.Forbidden)
            return Forbid();
        return Ok(await _repository.GetShelvesAsync(scope.UnitIds));
    }

    [HttpGet("shelves/{id}")]
    public async Task<IActionResult> GetShelfById(long id)
    {
        var result = await _repository.GetShelfByIdAsync(id);
        return result != null ? Ok(result) : NotFound();
    }

    [HttpPost("shelves")]
    public async Task<IActionResult> CreateShelf([FromBody] PhysicalShelf shelf)
    {
        if (shelf.UnitId is null or <= 0)
            return BadRequest(new { message = "Trường dữ liệu này không được để trống", field = "unitId" });

        var scope = await ResolveUnitScopeAsync(shelf.UnitId);
        if (scope.Forbidden || (scope.UnitIds != null && !scope.UnitIds.Contains(shelf.UnitId.Value)))
            return Forbid();

        var id = await _repository.CreateShelfAsync(shelf);
        shelf.Id = id;
        return CreatedAtAction(nameof(GetShelfById), new { id }, shelf);
    }

    [HttpPut("shelves/{id}")]
    public async Task<IActionResult> UpdateShelf(long id, [FromBody] PhysicalShelf shelf)
    {
        if (id != shelf.Id) return BadRequest();
        if (shelf.UnitId is null or <= 0)
            return BadRequest(new { message = "Trường dữ liệu này không được để trống", field = "unitId" });

        var scope = await ResolveUnitScopeAsync(shelf.UnitId);
        if (scope.Forbidden || (scope.UnitIds != null && !scope.UnitIds.Contains(shelf.UnitId.Value)))
            return Forbid();

        return await _repository.UpdateShelfAsync(shelf) ? NoContent() : NotFound();
    }

    [HttpDelete("shelves/{id}")]
    public async Task<IActionResult> DeleteShelf(long id) =>
        await _repository.DeleteShelfAsync(id) ? NoContent() : NotFound();

    // --- FLOOR ---
    /// <summary>
    /// Danh sách tầng thuộc các kệ của đơn vị hiện tại.
    /// </summary>
    [HttpGet("floors")]
    public async Task<IActionResult> GetFloorsByUnit([FromQuery] long? unitId = null)
    {
        var scope = await ResolveUnitScopeAsync(unitId);
        if (scope.Forbidden)
            return Forbid();
        return Ok(await _repository.GetFloorsByUnitIdsAsync(scope.UnitIds));
    }

    [HttpGet("shelves/{shelfId}/floors")]
    public async Task<IActionResult> GetFloors(long shelfId) => Ok(await _repository.GetFloorsByShelfIdAsync(shelfId));

    [HttpGet("floors/{id:long}")]
    public async Task<IActionResult> GetFloorById(long id)
    {
        var result = await _repository.GetFloorByIdAsync(id);
        return result != null ? Ok(result) : NotFound();
    }

    [HttpPost("floors")]
    public async Task<IActionResult> CreateFloor([FromBody] PhysicalFloor floor)
    {
        var id = await _repository.CreateFloorAsync(floor);
        floor.Id = id;
        return CreatedAtAction(nameof(GetFloorById), new { id }, floor);
    }

    [HttpPut("floors/{id:long}")]
    public async Task<IActionResult> UpdateFloor(long id, [FromBody] PhysicalFloor floor)
    {
        if (id != floor.Id) return BadRequest();
        return await _repository.UpdateFloorAsync(floor) ? NoContent() : NotFound();
    }

    [HttpDelete("floors/{id:long}")]
    public async Task<IActionResult> DeleteFloor(long id) =>
        await _repository.DeleteFloorAsync(id) ? NoContent() : NotFound();

    // --- BOX ---
    /// <summary>
    /// Danh sách hộp thuộc các tầng/kệ của đơn vị hiện tại.
    /// </summary>
    [HttpGet("boxes")]
    public async Task<IActionResult> GetBoxesByUnit([FromQuery] long? unitId = null)
    {
        var scope = await ResolveUnitScopeAsync(unitId);
        if (scope.Forbidden)
            return Forbid();
        return Ok(await _repository.GetBoxesByUnitIdsAsync(scope.UnitIds));
    }

    [HttpGet("floors/{floorId:long}/boxes")]
    public async Task<IActionResult> GetBoxes(long floorId) => Ok(await _repository.GetBoxesByFloorIdAsync(floorId));

    [HttpGet("boxes/{id:long}")]
    public async Task<IActionResult> GetBoxById(long id)
    {
        var result = await _repository.GetBoxByIdAsync(id);
        return result != null ? Ok(result) : NotFound();
    }

    [HttpPost("boxes")]
    public async Task<IActionResult> CreateBox([FromBody] PhysicalBox box)
    {
        var id = await _repository.CreateBoxAsync(box);
        box.Id = id;
        return CreatedAtAction(nameof(GetBoxById), new { id }, box);
    }

    [HttpPut("boxes/{id:long}")]
    public async Task<IActionResult> UpdateBox(long id, [FromBody] PhysicalBox box)
    {
        if (id != box.Id) return BadRequest();
        return await _repository.UpdateBoxAsync(box) ? NoContent() : NotFound();
    }

    [HttpDelete("boxes/{id:long}")]
    public async Task<IActionResult> DeleteBox(long id) =>
        await _repository.DeleteBoxAsync(id) ? NoContent() : NotFound();

    /// <summary>
    /// Xác định phạm vi đơn vị để lọc.
    /// - Có unitId → chỉ đúng đơn vị đó (không gồm đơn vị con).
    /// - Admin không truyền unitId → null (toàn hệ thống).
    /// - Non-admin không unitId → chỉ đơn vị JWT hiện tại (không gồm con).
    /// </summary>
    private async Task<(IReadOnlyList<long>? UnitIds, bool Forbidden)> ResolveUnitScopeAsync(long? requestedUnitId)
    {
        var allowed = await GetAllowedUnitIdsAsync();

        if (requestedUnitId.HasValue && requestedUnitId.Value > 0)
        {
            if (allowed != null && !allowed.Contains(requestedUnitId.Value))
                return (null, true);
            // Chỉ đúng 1 đơn vị — không expand cây con
            return (new[] { requestedUnitId.Value }, false);
        }

        return (allowed, false);
    }

    private Task<List<long>?> GetAllowedUnitIdsAsync()
    {
        var isAdmin = User.IsInRole("ADMIN")
            || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        if (isAdmin)
            return Task.FromResult<List<long>?>(null);

        // Chỉ đơn vị hiện tại trên JWT — không lấy đơn vị con
        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        if (!string.IsNullOrEmpty(unitIdClaim) && long.TryParse(unitIdClaim, out var userUnitId) && userUnitId > 0)
            return Task.FromResult<List<long>?>(new List<long> { userUnitId });

        var fallbackUnitIds = GetAuthorizedUnitIds();
        if (fallbackUnitIds != null && fallbackUnitIds.Count > 0)
            return Task.FromResult<List<long>?>(fallbackUnitIds.Distinct().ToList());

        return Task.FromResult<List<long>?>(new List<long> { -1 });
    }

    private List<long>? GetAuthorizedUnitIds()
    {
        var isAdmin = User.IsInRole("ADMIN")
            || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        if (isAdmin)
            return null;

        var unitRolesClaim = User.FindFirst("unit_roles")?.Value;
        if (string.IsNullOrEmpty(unitRolesClaim))
            return new List<long>();

        try
        {
            var unitRoles = JsonSerializer.Deserialize<List<UnitRoleDto>>(unitRolesClaim, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return unitRoles?.Select(ur => ur.UnitId).Distinct().ToList() ?? new List<long>();
        }
        catch
        {
            return new List<long>();
        }
    }

    private sealed class UnitRoleDto
    {
        public long UnitId { get; set; }
    }
}
