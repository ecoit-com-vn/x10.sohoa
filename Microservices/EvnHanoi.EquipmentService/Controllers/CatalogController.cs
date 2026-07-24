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

    // ─── CATALOG TYPE endpoints ──────────────────────────────

    /// <summary>Danh sách tất cả loại danh mục (dùng cho dropdown).</summary>
    [HttpGet("types")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetCatalogTypes()
    {
        var result = await _catalogRepository.GetCatalogTypesAsync();
        return Ok(result);
    }

    /// <summary>Lookup alias — giống GetCatalogTypes, dùng cho lookup dropdown.</summary>
    [HttpGet("types/lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> LookupCatalogTypes()
    {
        var result = await _catalogRepository.GetCatalogTypesAsync();
        return Ok(result);
    }

    /// <summary>Lấy 1 CatalogType theo Id.</summary>
    [HttpGet("types/{id:long}")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetCatalogTypeById(long id)
    {
        var result = await _catalogRepository.GetCatalogTypeByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>Lấy 1 CatalogType theo Code.</summary>
    [HttpGet("types/code/{code}")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetCatalogTypeByCode(string code)
    {
        var result = await _catalogRepository.GetCatalogTypeByCodeAsync(code);
        if (result == null) return NotFound(new { message = $"Không tìm thấy loại danh mục với mã = {code}" });
        return Ok(result);
    }

    // ─── CATALOG endpoints ───────────────────────────────────

    /// <summary>
    /// Lookup danh mục đang Active — lọc theo catalogTypeId hoặc code (mã CatalogType, vd. EQUIPMENT_STATUS).
    /// Query param: catalogTypeId (long), code (string), keyword (string)
    /// </summary>
    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> Lookup(
        [FromQuery] long? catalogTypeId = null,
        [FromQuery] string? code = null,
        [FromQuery] string? keyword = null)
    {
        if (!catalogTypeId.HasValue && !string.IsNullOrWhiteSpace(code))
        {
            var catalogType = await _catalogRepository.GetCatalogTypeByCodeAsync(code);
            if (catalogType == null)
                return Ok(Array.Empty<Catalog>());

            catalogTypeId = catalogType.Id;
        }

        long? unitId = GetUnitIdFromClaims();
        var result = await _catalogRepository.GetAllAsync(catalogTypeId, keyword, 1, unitId);
        return Ok(result);
    }

    /// <summary>
    /// Danh sách danh mục có phân trang.
    /// Query params: page, pageSize, catalogTypeId (long), keyword, status
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] long? catalogTypeId = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int? status = null,
        [FromQuery] long? unitId = null)
    {
        var catalogType = catalogTypeId.HasValue
            ? await _catalogRepository.GetCatalogTypeByIdAsync(catalogTypeId.Value)
            : null;
        var isPhong = catalogType?.Code == "PHONG";
        if (isPhong)
        {
            if (!unitId.HasValue || unitId <= 0)
                return BadRequest(new { message = "Vui lòng chọn một đơn vị." });
            if (!CanAccessPhongUnit(unitId.Value))
                return Forbid();
        }

        long? effectiveUnitId = isPhong ? unitId : unitId ?? GetUnitIdFromClaims();
        var (items, totalCount) = await _catalogRepository.GetPagedAsync(
            page, pageSize, catalogTypeId, keyword, status, effectiveUnitId,
            strictUnitFilter: isPhong);
        return Ok(new { items, totalCount, page, pageSize });
    }

    /// <summary>Lấy 1 danh mục theo Id.</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _catalogRepository.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>Tạo mới danh mục. Body phải có CatalogTypeId (long).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Catalog catalog)
    {
        if (string.IsNullOrWhiteSpace(catalog.Code) || string.IsNullOrWhiteSpace(catalog.Name))
            return BadRequest(new { message = "Mã danh mục và Tên danh mục là bắt buộc." });

        if (catalog.CatalogTypeId <= 0)
            return BadRequest(new { message = "CatalogTypeId không hợp lệ." });

        // Xác minh CatalogTypeId tồn tại
        var catalogType = await _catalogRepository.GetCatalogTypeByIdAsync(catalog.CatalogTypeId);
        if (catalogType == null)
            return BadRequest(new { message = $"Loại danh mục với Id '{catalog.CatalogTypeId}' không tồn tại." });

        // Kiểm tra trùng mã trong cùng nhóm CatalogTypeId
        var existing = await _catalogRepository.GetByCodeAsync(catalog.CatalogTypeId, catalog.Code);
        if (existing != null)
            return BadRequest(new { message = $"Mã danh mục '{catalog.Code}' đã tồn tại trong nhóm '{catalogType.Name}'." });

        if (catalogType.Code == "PHONG")
        {
            if (!catalog.UnitId.HasValue || catalog.UnitId <= 0)
                return BadRequest(new { message = "Đơn vị là bắt buộc.", errors = new { unitId = "Đơn vị là bắt buộc" } });
            if (!CanAccessPhongUnit(catalog.UnitId.Value))
                return Forbid();
        }
        else
        {
            catalog.UnitId = catalog.UnitId.HasValue ? GetUnitIdFromClaims() : null;
        }
        catalog.CreatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";
        catalog.CreatedAt = DateTime.UtcNow;

        var id = await _catalogRepository.CreateAsync(catalog);
        catalog.Id = id;
        return CreatedAtAction(nameof(GetById), new { id }, catalog);
    }

    /// <summary>Cập nhật danh mục. Body phải có CatalogTypeId (long).</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Catalog catalog)
    {
        if (id != catalog.Id)
            return BadRequest(new { message = "ID không trùng khớp." });

        if (string.IsNullOrWhiteSpace(catalog.Code) || string.IsNullOrWhiteSpace(catalog.Name))
            return BadRequest(new { message = "Mã danh mục và Tên danh mục là bắt buộc." });

        if (catalog.CatalogTypeId <= 0)
            return BadRequest(new { message = "CatalogTypeId không hợp lệ." });

        // Xác minh CatalogTypeId tồn tại
        var catalogType = await _catalogRepository.GetCatalogTypeByIdAsync(catalog.CatalogTypeId);
        if (catalogType == null)
            return BadRequest(new { message = $"Loại danh mục với Id '{catalog.CatalogTypeId}' không tồn tại." });

        // Kiểm tra trùng mã với bản ghi khác trong cùng nhóm
        var existing = await _catalogRepository.GetByCodeAsync(catalog.CatalogTypeId, catalog.Code);
        if (existing != null && existing.Id != id)
            return BadRequest(new { message = $"Mã danh mục '{catalog.Code}' đã được sử dụng bởi bản ghi khác trong nhóm '{catalogType.Name}'." });

        var dbCatalog = await _catalogRepository.GetByIdAsync(id);
        if (dbCatalog == null) return NotFound();

        if (catalogType.Code == "PHONG")
        {
            if (!catalog.UnitId.HasValue || catalog.UnitId <= 0)
                return BadRequest(new { message = "Đơn vị là bắt buộc.", errors = new { unitId = "Đơn vị là bắt buộc" } });
            if (!CanAccessPhongUnit(catalog.UnitId.Value))
                return Forbid();
        }
        else
        {
            catalog.UnitId = catalog.UnitId.HasValue ? GetUnitIdFromClaims() : null;
        }
        catalog.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";

        var success = await _catalogRepository.UpdateAsync(catalog);
        if (!success) return NotFound();
        return NoContent();
    }

    /// <summary>Xóa danh mục (kiểm tra không có con).</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        if (await _catalogRepository.HasChildrenAsync(id))
            return BadRequest(new { message = "Không thể xóa danh mục này vì đang có các danh mục con tham chiếu tới." });

        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";
        var success = await _catalogRepository.DeleteAsync(id, username);
        if (!success) return NotFound();
        return NoContent();
    }

    /// <summary>Khóa danh mục (Status = 0).</summary>
    [HttpPost("{id:long}/lock")]
    public async Task<IActionResult> Lock(long id)
    {
        var catalog = await _catalogRepository.GetByIdAsync(id);
        if (catalog == null) return NotFound();

        catalog.Status = 0;
        catalog.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";

        var success = await _catalogRepository.UpdateAsync(catalog);
        if (!success) return StatusCode(500, new { message = "Không thể cập nhật trạng thái danh mục." });

        return Ok(new { message = "Ngừng hoạt động danh mục thành công.", status = 0 });
    }

    /// <summary>Mở khóa danh mục (Status = 1).</summary>
    [HttpPost("{id:long}/unlock")]
    public async Task<IActionResult> Unlock(long id)
    {
        var catalog = await _catalogRepository.GetByIdAsync(id);
        if (catalog == null) return NotFound();

        catalog.Status = 1;
        catalog.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";

        var success = await _catalogRepository.UpdateAsync(catalog);
        if (!success) return StatusCode(500, new { message = "Không thể cập nhật trạng thái danh mục." });

        return Ok(new { message = "Kích hoạt danh mục thành công.", status = 1 });
    }

    // ─── Helpers ─────────────────────────────────────────────

    private long? GetUnitIdFromClaims()
    {
        var claim = User.FindFirst("unit_id")?.Value;
        return long.TryParse(claim, out var unitId) ? unitId : null;
    }

    private bool CanAccessPhongUnit(long unitId)
    {
        var isAdmin = User.IsInRole("ADMIN") ||
            User.Claims.Any(c => c.Type == System.Security.Claims.ClaimTypes.Role &&
                                 string.Equals(c.Value, "ADMIN", StringComparison.OrdinalIgnoreCase));
        if (isAdmin) return true;

        var currentUnitId = GetUnitIdFromClaims();
        return currentUnitId.HasValue && currentUnitId.Value == unitId;
    }
}
