using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/catalog/private")]
public class PrivateCatalogController : ControllerBase
{
    private readonly ICatalogRepository _catalogRepository;

    public PrivateCatalogController(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetPrivateCatalogTypes(
        [FromQuery] string? keyword = null,
        [FromQuery] int? status = null)
    {
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";
        var result = await _catalogRepository.GetCatalogTypesFilteredAsync(true, keyword, status, username);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";
        var result = await _catalogRepository.GetCatalogTypeByIdFilteredAsync(id, true, username);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CatalogType catalogType)
    {
        if (string.IsNullOrWhiteSpace(catalogType.Code) || string.IsNullOrWhiteSpace(catalogType.Name))
            return BadRequest(new { message = "Mã loại danh mục và Tên loại danh mục là bắt buộc." });

        // Verify if Code already exists
        var existing = await _catalogRepository.GetCatalogTypeByCodeAsync(catalogType.Code);
        if (existing != null)
            return BadRequest(new { message = $"Mã loại danh mục '{catalogType.Code}' đã tồn tại." });

        catalogType.IsPrivate = true;
        catalogType.CreatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";
        catalogType.CreatedAt = DateTime.UtcNow;

        var id = await _catalogRepository.CreateCatalogTypeAsync(catalogType);
        catalogType.Id = id;
        return CreatedAtAction(nameof(GetById), new { id }, catalogType);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] CatalogType catalogType)
    {
        if (id != catalogType.Id)
            return BadRequest(new { message = "ID không trùng khớp." });

        if (string.IsNullOrWhiteSpace(catalogType.Code) || string.IsNullOrWhiteSpace(catalogType.Name))
            return BadRequest(new { message = "Mã loại danh mục và Tên loại danh mục là bắt buộc." });

        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";
        // Verify if Code already exists on another CatalogType
        var existing = await _catalogRepository.GetCatalogTypeByCodeAsync(catalogType.Code);
        if (existing != null && existing.Id != id)
            return BadRequest(new { message = $"Mã loại danh mục '{catalogType.Code}' đã được sử dụng bởi bản ghi khác." });

        var dbType = await _catalogRepository.GetCatalogTypeByIdFilteredAsync(id, true, username);
        if (dbType == null) return NotFound();

        catalogType.IsPrivate = true;

        var success = await _catalogRepository.UpdateCatalogTypeAsync(catalogType);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";
        var dbType = await _catalogRepository.GetCatalogTypeByIdFilteredAsync(id, true, username);
        if (dbType == null) return NotFound();

        var success = await _catalogRepository.DeleteCatalogTypeAsync(id, username);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:long}/lock")]
    public async Task<IActionResult> Lock(long id)
    {
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";
        var dbType = await _catalogRepository.GetCatalogTypeByIdFilteredAsync(id, true, username);
        if (dbType == null) return NotFound();

        dbType.Status = 0;
        var success = await _catalogRepository.UpdateCatalogTypeAsync(dbType);
        if (!success) return StatusCode(500, new { message = "Không thể cập nhật trạng thái loại danh mục." });

        return Ok(new { message = "Ngừng hoạt động loại danh mục thành công.", status = 0 });
    }

    [HttpPost("{id:long}/unlock")]
    public async Task<IActionResult> Unlock(long id)
    {
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "system";
        var dbType = await _catalogRepository.GetCatalogTypeByIdFilteredAsync(id, true, username);
        if (dbType == null) return NotFound();

        dbType.Status = 1;
        var success = await _catalogRepository.UpdateCatalogTypeAsync(dbType);
        if (!success) return StatusCode(500, new { message = "Không thể cập nhật trạng thái loại danh mục." });

        return Ok(new { message = "Kích hoạt loại danh mục thành công.", status = 1 });
    }
}
