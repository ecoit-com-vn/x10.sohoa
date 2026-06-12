using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
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

    [HttpGet("types")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetCatalogTypes()
    {
        var result = await _catalogRepository.GetCatalogTypesAsync();
        return Ok(result);
    }

    [HttpGet("types/lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> LookupCatalogTypes()
    {
        var result = await _catalogRepository.GetCatalogTypesAsync();
        return Ok(result);
    }

    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> Lookup([FromQuery] string? catalogType = null, [FromQuery] string? keyword = null)
    {
        long? unitId = null;
        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        if (long.TryParse(unitIdClaim, out var parsedUnitId))
        {
            unitId = parsedUnitId;
        }

        // Lookup only retrieves Active (status = 1) items
        var result = await _catalogRepository.GetAllAsync(catalogType, keyword, 1, unitId);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] string? catalogType = null, 
        [FromQuery] string? keyword = null, 
        [FromQuery] int? status = null)
    {
        long? unitId = null;
        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        if (long.TryParse(unitIdClaim, out var parsedUnitId))
        {
            unitId = parsedUnitId;
        }

        var (items, totalCount) = await _catalogRepository.GetPagedAsync(page, pageSize, catalogType, keyword, status, unitId);
        return Ok(new { items, totalCount, page, pageSize });
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
        if (string.IsNullOrWhiteSpace(catalog.Code) || string.IsNullOrWhiteSpace(catalog.Name))
        {
            return BadRequest(new { message = "Mã danh mục và Tên danh mục là bắt buộc." });
        }

        // Kiểm tra trùng mã trong cùng nhóm CatalogType
        var existing = await _catalogRepository.GetByCodeAsync(catalog.CatalogType, catalog.Code);
        if (existing != null)
        {
            return BadRequest(new { message = $"Mã danh mục '{catalog.Code}' đã tồn tại trong nhóm '{catalog.CatalogType}'." });
        }

        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        if (catalog.UnitId.HasValue && long.TryParse(unitIdClaim, out var unitId))
        {
            catalog.UnitId = unitId;
        }
        else
        {
            catalog.UnitId = null;
        }

        catalog.CreatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";
        catalog.CreatedAt = DateTime.UtcNow;

        var id = await _catalogRepository.CreateAsync(catalog);
        catalog.Id = id;
        return CreatedAtAction(nameof(GetById), new { id = id }, catalog);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] Catalog catalog)
    {
        if (id != catalog.Id) return BadRequest(new { message = "ID không trùng khớp." });

        if (string.IsNullOrWhiteSpace(catalog.Code) || string.IsNullOrWhiteSpace(catalog.Name))
        {
            return BadRequest(new { message = "Mã danh mục và Tên danh mục là bắt buộc." });
        }

        // Kiểm tra trùng mã đối với bản ghi khác
        var existing = await _catalogRepository.GetByCodeAsync(catalog.CatalogType, catalog.Code);
        if (existing != null && existing.Id != id)
        {
            return BadRequest(new { message = $"Mã danh mục '{catalog.Code}' đã được sử dụng bởi bản ghi khác trong nhóm '{catalog.CatalogType}'." });
        }

        var dbCatalog = await _catalogRepository.GetByIdAsync(id);
        if (dbCatalog == null) return NotFound();

        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        if (catalog.UnitId.HasValue && long.TryParse(unitIdClaim, out var unitId))
        {
            catalog.UnitId = unitId;
        }
        else
        {
            catalog.UnitId = null;
        }

        catalog.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";

        var success = await _catalogRepository.UpdateAsync(catalog);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        // Kiểm tra xem danh mục có danh mục con (parent_id) nào tham chiếu tới không
        if (await _catalogRepository.HasChildrenAsync(id))
        {
            return BadRequest(new { message = "Không thể xóa danh mục này vì đang có các danh mục con tham chiếu tới." });
        }

        var success = await _catalogRepository.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("{id}/lock")]
    public async Task<IActionResult> Lock(long id)
    {
        var catalog = await _catalogRepository.GetByIdAsync(id);
        if (catalog == null) return NotFound();

        catalog.Status = 0; // Locked
        catalog.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";

        var success = await _catalogRepository.UpdateAsync(catalog);
        if (!success) return StatusCode(500, new { message = "Không thể cập nhật trạng thái danh mục." });

        return Ok(new { message = "Đã khóa danh mục thành công.", status = 0 });
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock(long id)
    {
        var catalog = await _catalogRepository.GetByIdAsync(id);
        if (catalog == null) return NotFound();

        catalog.Status = 1; // Active
        catalog.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";

        var success = await _catalogRepository.UpdateAsync(catalog);
        if (!success) return StatusCode(500, new { message = "Không thể cập nhật trạng thái danh mục." });

        return Ok(new { message = "Đã mở khóa danh mục thành công.", status = 1 });
    }
}

