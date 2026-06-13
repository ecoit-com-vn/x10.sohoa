using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/catalog/physical-status")]
public class PhysicalStatusController : ControllerBase
{
    private readonly ICatalogRepository _catalogRepository;
    private const string CatalogTypeCode = "TINH_TRANG_VAT_LY";

    public PhysicalStatusController(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    private async Task<long> GetCatalogTypeIdAsync()
    {
        var typeObj = await _catalogRepository.GetCatalogTypeByCodeAsync(CatalogTypeCode);
        return typeObj?.Id ?? 0;
    }

    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> Lookup([FromQuery] string? keyword = null)
    {
        long typeId = await GetCatalogTypeIdAsync();
        long? unitId = GetUnitIdFromClaims();
        var result = await _catalogRepository.GetAllAsync(typeId, keyword, 1, unitId);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] int? status = null)
    {
        long typeId = await GetCatalogTypeIdAsync();
        long? unitId = GetUnitIdFromClaims();
        var (items, totalCount) = await _catalogRepository.GetPagedAsync(page, pageSize, typeId, keyword, status, unitId);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _catalogRepository.GetByIdAsync(id);
        if (result == null || result.CatalogTypeId != await GetCatalogTypeIdAsync()) 
            return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Catalog catalog)
    {
        if (string.IsNullOrWhiteSpace(catalog.Code) || string.IsNullOrWhiteSpace(catalog.Name))
            return BadRequest(new { message = "Mã danh mục và Tên danh mục là bắt buộc." });

        catalog.CatalogTypeId = await GetCatalogTypeIdAsync();
        if (catalog.CatalogTypeId <= 0)
            return BadRequest(new { message = $"Không tìm thấy loại danh mục '{CatalogTypeCode}' trong hệ thống." });

        // Verify if Code already exists
        var existing = await _catalogRepository.GetByCodeAsync(catalog.CatalogTypeId, catalog.Code);
        if (existing != null)
            return BadRequest(new { message = $"Mã danh mục '{catalog.Code}' đã tồn tại trong nhóm này." });

        catalog.UnitId = catalog.UnitId.HasValue ? GetUnitIdFromClaims() : null;
        catalog.CreatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";
        catalog.CreatedAt = DateTime.UtcNow;

        var id = await _catalogRepository.CreateAsync(catalog);
        catalog.Id = id;
        return CreatedAtAction(nameof(GetById), new { id }, catalog);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Catalog catalog)
    {
        if (id != catalog.Id)
            return BadRequest(new { message = "ID không trùng khớp." });

        if (string.IsNullOrWhiteSpace(catalog.Code) || string.IsNullOrWhiteSpace(catalog.Name))
            return BadRequest(new { message = "Mã danh mục và Tên danh mục là bắt buộc." });

        catalog.CatalogTypeId = await GetCatalogTypeIdAsync();
        if (catalog.CatalogTypeId <= 0)
            return BadRequest(new { message = $"Không tìm thấy loại danh mục '{CatalogTypeCode}' trong hệ thống." });

        var existing = await _catalogRepository.GetByCodeAsync(catalog.CatalogTypeId, catalog.Code);
        if (existing != null && existing.Id != id)
            return BadRequest(new { message = $"Mã danh mục '{catalog.Code}' đã được sử dụng bởi bản ghi khác trong nhóm." });

        var dbCatalog = await _catalogRepository.GetByIdAsync(id);
        if (dbCatalog == null || dbCatalog.CatalogTypeId != catalog.CatalogTypeId) 
            return NotFound();

        catalog.UnitId = catalog.UnitId.HasValue ? GetUnitIdFromClaims() : null;
        catalog.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";

        var success = await _catalogRepository.UpdateAsync(catalog);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var dbCatalog = await _catalogRepository.GetByIdAsync(id);
        if (dbCatalog == null || dbCatalog.CatalogTypeId != await GetCatalogTypeIdAsync()) 
            return NotFound();

        if (await _catalogRepository.HasChildrenAsync(id))
            return BadRequest(new { message = "Không thể xóa danh mục này vì đang có các danh mục con tham chiếu tới." });

        var success = await _catalogRepository.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:long}/lock")]
    public async Task<IActionResult> Lock(long id)
    {
        var catalog = await _catalogRepository.GetByIdAsync(id);
        if (catalog == null || catalog.CatalogTypeId != await GetCatalogTypeIdAsync()) 
            return NotFound();

        catalog.Status = 0;
        catalog.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";

        var success = await _catalogRepository.UpdateAsync(catalog);
        if (!success) return StatusCode(500, new { message = "Không thể cập nhật trạng thái danh mục." });

        return Ok(new { message = "Đã khóa danh mục thành công.", status = 0 });
    }

    [HttpPost("{id:long}/unlock")]
    public async Task<IActionResult> Unlock(long id)
    {
        var catalog = await _catalogRepository.GetByIdAsync(id);
        if (catalog == null || catalog.CatalogTypeId != await GetCatalogTypeIdAsync()) 
            return NotFound();

        catalog.Status = 1;
        catalog.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";

        var success = await _catalogRepository.UpdateAsync(catalog);
        if (!success) return StatusCode(500, new { message = "Không thể cập nhật trạng thái danh mục." });

        return Ok(new { message = "Đã mở khóa danh mục thành công.", status = 1 });
    }

    private long? GetUnitIdFromClaims()
    {
        var claim = User.FindFirst("unit_id")?.Value;
        return long.TryParse(claim, out var unitId) ? unitId : null;
    }
}
