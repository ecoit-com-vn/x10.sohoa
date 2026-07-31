using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

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
        if (catalogType?.Code == "PHONG") return NotFound();
        var isUnitScoped = catalogType?.Code == "MUC_LUC" || catalogType?.Code == "PHONG";
        if (isUnitScoped)
        {
            if (!unitId.HasValue || unitId <= 0)
                return BadRequest(new { statusCode = 400, message = "Dữ liệu đầu vào không hợp lệ.", errors = new { unitId = "Vui lòng chọn một đơn vị" } });
            if (!CanAccessUnit(unitId.Value)) return Forbid();
        }
        long? effectiveUnitId = unitId ?? GetUnitIdFromClaims();
        var (items, totalCount) = await _catalogRepository.GetPagedAsync(
            page, pageSize, catalogTypeId, keyword, status, effectiveUnitId,
            strictUnitFilter: isUnitScoped);
        return Ok(new { items, totalCount, page, pageSize });
    }

    /// <summary>Lấy 1 danh mục theo Id.</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _catalogRepository.GetByIdAsync(id);
        if (result == null) return NotFound();
        if (await IsPhongAsync(result.CatalogTypeId)) return NotFound();
        if (await IsMucLucAsync(result.CatalogTypeId) &&
            (!result.UnitId.HasValue || !CanAccessUnit(result.UnitId.Value)))
            return Forbid();
        return Ok(result);
    }

    /// <summary>Tạo mới danh mục. Body phải có CatalogTypeId (long).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Catalog catalog)
    {
        catalog.Code = catalog.Code?.Trim() ?? string.Empty;
        catalog.Name = catalog.Name?.Trim() ?? string.Empty;
        catalog.Description = catalog.Description?.Trim();
        if (string.IsNullOrWhiteSpace(catalog.Code) || string.IsNullOrWhiteSpace(catalog.Name))
            return BadRequest(new { message = "Mã danh mục và Tên danh mục là bắt buộc." });

        if (catalog.CatalogTypeId <= 0)
            return BadRequest(new { message = "CatalogTypeId không hợp lệ." });

        // Xác minh CatalogTypeId tồn tại
        var catalogType = await _catalogRepository.GetCatalogTypeByIdAsync(catalog.CatalogTypeId);
        if (catalogType == null)
            return BadRequest(new { message = $"Loại danh mục với Id '{catalog.CatalogTypeId}' không tồn tại." });
        if (catalogType.Code == "PHONG") return NotFound();

        var isUnitScoped = catalogType.Code == "MUC_LUC" || catalogType.Code == "PHONG";
        if (isUnitScoped && (!catalog.UnitId.HasValue || catalog.UnitId <= 0))
            return BadRequest(new { statusCode = 400, message = "Dữ liệu đầu vào không hợp lệ.", errors = new { unitId = "Đơn vị là bắt buộc" } });
        if (isUnitScoped && !CanAccessUnit(catalog.UnitId!.Value)) return Forbid();

        var existing = isUnitScoped
            ? await _catalogRepository.GetByCodeForUnitAsync(catalog.CatalogTypeId, catalog.Code, catalog.UnitId!.Value)
            : await _catalogRepository.GetByCodeAsync(catalog.CatalogTypeId, catalog.Code);
        if (existing != null)
            return BadRequest(new { statusCode = 400, message = "Dữ liệu đầu vào không hợp lệ.", errors = new { code = $"Mã danh mục '{catalog.Code}' đã tồn tại" } });

        if (isUnitScoped)
        {
            var parentError = await ValidateParentAsync(catalog, null);
            if (parentError != null)
                return BadRequest(new { statusCode = 400, message = "Dữ liệu đầu vào không hợp lệ.", errors = new { parentId = parentError } });
        }

        if (!isUnitScoped)
            catalog.UnitId = catalog.UnitId.HasValue ? GetUnitIdFromClaims() : null;

        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";
        var deletedCatalog = await _catalogRepository.GetDeletedByCodeAsync(
            catalog.CatalogTypeId,
            catalog.Code,
            catalog.UnitId,
            strictUnitFilter: isUnitScoped);
        if (deletedCatalog != null)
        {
            catalog.Id = deletedCatalog.Id;
            catalog.CreatedAt = deletedCatalog.CreatedAt;
            catalog.CreatedBy = deletedCatalog.CreatedBy;
            catalog.UpdatedBy = username;

            if (await _catalogRepository.RestoreAsync(catalog))
                return CreatedAtAction(nameof(GetById), new { id = catalog.Id }, catalog);

            var concurrentCatalog = isUnitScoped
                ? await _catalogRepository.GetByCodeForUnitAsync(catalog.CatalogTypeId, catalog.Code, catalog.UnitId!.Value)
                : await _catalogRepository.GetByCodeAsync(catalog.CatalogTypeId, catalog.Code);
            if (concurrentCatalog != null)
                return CatalogCodeConflict(catalog.Code);

            return Conflict(new { message = "Không thể khôi phục danh mục. Vui lòng thử lại." });
        }

        catalog.CreatedBy = username;
        catalog.CreatedAt = DateTime.UtcNow;

        try
        {
            var id = await _catalogRepository.CreateAsync(catalog);
            catalog.Id = id;
            return CreatedAtAction(nameof(GetById), new { id }, catalog);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return CatalogCodeConflict(catalog.Code);
        }
    }

    private static BadRequestObjectResult CatalogCodeConflict(string code) =>
        new(new
        {
            statusCode = 400,
            message = "Dữ liệu đầu vào không hợp lệ.",
            errors = new { code = $"Mã danh mục '{code}' đã tồn tại" }
        });

    /// <summary>Cập nhật danh mục. Body phải có CatalogTypeId (long).</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Catalog catalog)
    {
        catalog.Code = catalog.Code?.Trim() ?? string.Empty;
        catalog.Name = catalog.Name?.Trim() ?? string.Empty;
        catalog.Description = catalog.Description?.Trim();
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
        if (catalogType.Code == "PHONG") return NotFound();

        var dbCatalog = await _catalogRepository.GetByIdAsync(id);
        if (dbCatalog == null) return NotFound();
        if (await IsMucLucAsync(dbCatalog.CatalogTypeId) &&
            (!dbCatalog.UnitId.HasValue || !CanAccessUnit(dbCatalog.UnitId.Value)))
            return Forbid();
        if (dbCatalog.CatalogTypeId != catalog.CatalogTypeId)
            return BadRequest(new { message = "Không được thay đổi loại danh mục." });

        var isUnitScoped = catalogType.Code == "MUC_LUC" || catalogType.Code == "PHONG";
        if (isUnitScoped && (!catalog.UnitId.HasValue || catalog.UnitId <= 0))
            return BadRequest(new { statusCode = 400, message = "Dữ liệu đầu vào không hợp lệ.", errors = new { unitId = "Đơn vị là bắt buộc" } });
        if (isUnitScoped && !CanAccessUnit(catalog.UnitId!.Value)) return Forbid();

        var existing = isUnitScoped
            ? await _catalogRepository.GetByCodeForUnitAsync(catalog.CatalogTypeId, catalog.Code, catalog.UnitId!.Value)
            : await _catalogRepository.GetByCodeAsync(catalog.CatalogTypeId, catalog.Code);
        if (existing != null && existing.Id != id)
            return BadRequest(new { statusCode = 400, message = "Dữ liệu đầu vào không hợp lệ.", errors = new { code = $"Mã danh mục '{catalog.Code}' đã tồn tại" } });

        if (isUnitScoped)
        {
            if (dbCatalog.UnitId != catalog.UnitId &&
                (dbCatalog.ParentId.HasValue || await _catalogRepository.HasChildrenAsync(id)))
                return BadRequest(new { statusCode = 400, message = "Dữ liệu đầu vào không hợp lệ.", errors = new { unitId = "Không thể đổi đơn vị khi danh mục đang có quan hệ cha-con" } });

            var parentError = await ValidateParentAsync(catalog, id);
            if (parentError != null)
                return BadRequest(new { statusCode = 400, message = "Dữ liệu đầu vào không hợp lệ.", errors = new { parentId = parentError } });
        }

        if (!isUnitScoped)
            catalog.UnitId = catalog.UnitId.HasValue ? GetUnitIdFromClaims() : null;
        catalog.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";

        var success = await _catalogRepository.UpdateAsync(catalog);
        if (!success) return NotFound();
        return NoContent();
    }

    /// <summary>Xóa danh mục (kiểm tra không có con).</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var catalog = await _catalogRepository.GetByIdAsync(id);
        if (catalog == null) return NotFound();
        if (await IsPhongAsync(catalog.CatalogTypeId)) return NotFound();
        if (await IsMucLucAsync(catalog.CatalogTypeId) &&
            (!catalog.UnitId.HasValue || !CanAccessUnit(catalog.UnitId.Value)))
            return Forbid();
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
        if (await IsPhongAsync(catalog.CatalogTypeId)) return NotFound();
        if (await IsMucLucAsync(catalog.CatalogTypeId) &&
            (!catalog.UnitId.HasValue || !CanAccessUnit(catalog.UnitId.Value)))
            return Forbid();

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
        if (await IsPhongAsync(catalog.CatalogTypeId)) return NotFound();
        if (await IsMucLucAsync(catalog.CatalogTypeId) &&
            (!catalog.UnitId.HasValue || !CanAccessUnit(catalog.UnitId.Value)))
            return Forbid();

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

    private bool CanAccessUnit(long unitId)
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c =>
            c.Type == System.Security.Claims.ClaimTypes.Role &&
            string.Equals(c.Value, "ADMIN", StringComparison.OrdinalIgnoreCase));
        if (isAdmin) return true;
        return GetUnitIdFromClaims() == unitId;
    }

    private async Task<bool> IsMucLucAsync(long catalogTypeId)
        => (await _catalogRepository.GetCatalogTypeByIdAsync(catalogTypeId))?.Code == "MUC_LUC";

    private async Task<bool> IsPhongAsync(long catalogTypeId)
        => (await _catalogRepository.GetCatalogTypeByIdAsync(catalogTypeId))?.Code == "PHONG";

    private async Task<string?> ValidateParentAsync(Catalog catalog, long? currentId)
    {
        if (!catalog.ParentId.HasValue) return null;
        if (currentId.HasValue && catalog.ParentId.Value == currentId.Value)
            return "Danh mục không thể là cha của chính nó";

        var parent = await _catalogRepository.GetByIdAsync(catalog.ParentId.Value);
        if (parent == null) return "Danh mục cha không tồn tại";
        if (parent.CatalogTypeId != catalog.CatalogTypeId)
            return "Danh mục cha không cùng loại";
        if (parent.UnitId != catalog.UnitId)
            return "Danh mục cha không cùng đơn vị";
        if (parent.Status != 1)
            return "Danh mục cha đang ngừng hoạt động";

        if (!currentId.HasValue) return null;
        var visited = new HashSet<long>();
        long? ancestorId = catalog.ParentId;
        while (ancestorId.HasValue && visited.Add(ancestorId.Value))
        {
            if (ancestorId.Value == currentId.Value)
                return "Quan hệ cha-con tạo thành vòng lặp";
            ancestorId = (await _catalogRepository.GetByIdAsync(ancestorId.Value))?.ParentId;
        }
        return ancestorId.HasValue ? "Dữ liệu cây hiện tại có vòng lặp" : null;
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
