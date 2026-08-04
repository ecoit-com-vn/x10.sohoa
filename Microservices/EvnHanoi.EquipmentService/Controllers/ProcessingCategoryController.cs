using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/catalog/processing-category")]
public class ProcessingCategoryController : ControllerBase
{
    private const string CatalogTypeCode = "PROCESSING_CATEGORY";
    private readonly ICatalogRepository _catalogRepository;

    public ProcessingCategoryController(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> Lookup([FromQuery] string? keyword = null)
    {
        var typeId = await GetCatalogTypeIdAsync();
        var result = await _catalogRepository.GetAllAsync(typeId, keyword, 1, GetUnitIdFromClaims());
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] int? status = null)
    {
        var typeId = await GetCatalogTypeIdAsync();
        var (items, totalCount) = await _catalogRepository.GetPagedAsync(
            page, pageSize, typeId, keyword, status, GetUnitIdFromClaims());
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
        catalog.Code = catalog.Code?.Trim() ?? string.Empty;
        catalog.Name = catalog.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(catalog.Code) || string.IsNullOrWhiteSpace(catalog.Name))
            return BadRequest(new { message = "Mã danh mục và Tên danh mục là bắt buộc." });

        catalog.CatalogTypeId = await GetCatalogTypeIdAsync();
        if (catalog.CatalogTypeId <= 0)
            return BadRequest(new { message = $"Không tìm thấy loại danh mục '{CatalogTypeCode}' trong hệ thống." });

        var existing = await _catalogRepository.GetByCodeAsync(catalog.CatalogTypeId, catalog.Code);
        if (existing != null)
            return CodeConflict(catalog.Code);

        catalog.UnitId = catalog.UnitId.HasValue ? GetUnitIdFromClaims() : null;
        catalog.Priority = catalog.Priority <= 0 ? 1 : catalog.Priority;
        var username = GetUsername();
        var deleted = await _catalogRepository.GetDeletedByCodeAsync(catalog.CatalogTypeId, catalog.Code);
        if (deleted != null)
        {
            catalog.Id = deleted.Id;
            catalog.CreatedAt = deleted.CreatedAt;
            catalog.CreatedBy = deleted.CreatedBy;
            catalog.UpdatedBy = username;
            catalog.IsDeleted = false;
            if (await _catalogRepository.RestoreAsync(catalog))
                return CreatedAtAction(nameof(GetById), new { id = catalog.Id }, catalog);

            if (await _catalogRepository.GetByCodeAsync(catalog.CatalogTypeId, catalog.Code) != null)
                return CodeConflict(catalog.Code);
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
            return CodeConflict(catalog.Code);
        }
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] Catalog catalog)
    {
        catalog.Code = catalog.Code?.Trim() ?? string.Empty;
        catalog.Name = catalog.Name?.Trim() ?? string.Empty;
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

        var current = await _catalogRepository.GetByIdAsync(id);
        if (current == null || current.CatalogTypeId != catalog.CatalogTypeId)
            return NotFound();

        catalog.UnitId = catalog.UnitId.HasValue ? GetUnitIdFromClaims() : null;
        catalog.Priority = catalog.Priority <= 0 ? 1 : catalog.Priority;
        catalog.UpdatedBy = GetUsername();

        return await _catalogRepository.UpdateAsync(catalog) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var current = await _catalogRepository.GetByIdAsync(id);
        if (current == null || current.CatalogTypeId != await GetCatalogTypeIdAsync())
            return NotFound();

        if (await _catalogRepository.HasChildrenAsync(id))
            return BadRequest(new { message = "Không thể xóa danh mục này vì đang có danh mục con tham chiếu tới." });

        return await _catalogRepository.DeleteAsync(id, GetUsername()) ? NoContent() : NotFound();
    }

    [HttpPost("{id:long}/lock")]
    public Task<IActionResult> Lock(long id) => UpdateStatus(id, 0, "Đã khóa danh mục thành công.");

    [HttpPost("{id:long}/unlock")]
    public Task<IActionResult> Unlock(long id) => UpdateStatus(id, 1, "Đã mở khóa danh mục thành công.");

    private async Task<IActionResult> UpdateStatus(long id, int status, string message)
    {
        var catalog = await _catalogRepository.GetByIdAsync(id);
        if (catalog == null || catalog.CatalogTypeId != await GetCatalogTypeIdAsync())
            return NotFound();

        catalog.Status = status;
        catalog.UpdatedBy = GetUsername();
        if (!await _catalogRepository.UpdateAsync(catalog))
            return StatusCode(500, new { message = "Không thể cập nhật trạng thái danh mục." });

        return Ok(new { message, status });
    }

    private async Task<long> GetCatalogTypeIdAsync()
    {
        var type = await _catalogRepository.GetCatalogTypeByCodeAsync(CatalogTypeCode);
        return type?.Id ?? 0;
    }

    private string GetUsername() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";

    private static BadRequestObjectResult CodeConflict(string code) =>
        new(new
        {
            statusCode = 400,
            message = "Dữ liệu đầu vào không hợp lệ.",
            errors = new { code = $"Mã danh mục '{code}' đã tồn tại" }
        });

    private long? GetUnitIdFromClaims()
    {
        var claim = User.FindFirst("unit_id")?.Value;
        return long.TryParse(claim, out var unitId) ? unitId : null;
    }
}
