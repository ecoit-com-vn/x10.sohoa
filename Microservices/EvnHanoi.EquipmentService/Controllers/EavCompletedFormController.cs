using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

[ApiController]
[Route("api/v1/eav-completed-forms")]
public class EavCompletedFormController : ControllerBase
{
    private readonly IEavFormTemplateRepository _repository;
    private readonly ICatalogRepository _catalogRepository;

    public EavCompletedFormController(
        IEavFormTemplateRepository repository,
        ICatalogRepository catalogRepository)
    {
        _repository = repository;
        _catalogRepository = catalogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetAllActive()
    {
        var templates = await _repository.GetCompletedFormsAsync();
        return Ok(templates);
    }

    /// <summary>
    /// Lookup tên danh mục phục vụ preview form hoàn thành — tránh gọi CatalogController (quyền khác).
    /// </summary>
    [HttpGet("catalog-options/{typeCode}")]
    public async Task<ActionResult<IEnumerable<object>>> GetCatalogOptions(string typeCode)
    {
        if (string.IsNullOrWhiteSpace(typeCode))
            return BadRequest(new { Message = "Mã loại danh mục không được để trống." });

        var catalogType = await _catalogRepository.GetCatalogTypeByCodeAsync(typeCode.Trim());
        if (catalogType == null)
            return Ok(Array.Empty<object>());

        var items = await _catalogRepository.GetAllAsync(catalogTypeId: catalogType.Id, status: 1);
        return Ok(items.Select(c => new { c.Code, c.Name }));
    }

    /// <summary>Lookup hạng mục HMAD cho màn sửa form hoàn thành.</summary>
    [HttpGet("hmad-categories")]
    public async Task<ActionResult<IEnumerable<object>>> GetHmadCategories()
    {
        var catalogType = await _catalogRepository.GetCatalogTypeByCodeAsync("HMAD");
        if (catalogType == null)
            return Ok(Array.Empty<object>());

        var items = await _catalogRepository.GetAllAsync(catalogTypeId: catalogType.Id, status: 1);
        return Ok(items.Select(c => new { c.Id, c.Code, c.Name }));
    }

    /// <summary>Lookup loại danh mục cho builder khi sửa form hoàn thành.</summary>
    [HttpGet("catalog-types")]
    public async Task<ActionResult<IEnumerable<object>>> GetCatalogTypesLookup()
    {
        var types = await _catalogRepository.GetCatalogTypesAsync();
        return Ok(types.Select(t => new { t.Id, t.Code, t.Name }));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EavFormTemplate>> GetById(Guid id)
    {
        var template = await _repository.GetByIdAsync(id);
        if (template == null)
            return NotFound(new { Message = $"Không tìm thấy biểu mẫu với ID = {id}" });

        return Ok(template);
    }

    [HttpPost("{id:guid}/lock")]
    public async Task<IActionResult> Lock(Guid id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Không tìm thấy biểu mẫu với ID = {id}" });

        existing.IsActive = false;
        await _repository.UpdateAsync(existing);
        return Ok(new { Message = "Khóa biểu mẫu thành công." });
    }

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Không tìm thấy biểu mẫu với ID = {id}" });

        existing.IsActive = true;
        await _repository.UpdateAsync(existing);
        return Ok(new { Message = "Mở khóa biểu mẫu thành công." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Inactivate(Guid id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Không tìm thấy biểu mẫu với ID = {id}" });

        existing.IsDeleted = true;
        await _repository.UpdateAsync(existing);
        await _repository.DeleteVersionsAsync(existing.Id);

        return NoContent();
    }

    [HttpGet("code/{code}/versions")]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetVersionsByCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest("Mã biểu mẫu không được để trống.");

        var versions = await _repository.GetVersionsByCodeAsync(code);
        return Ok(versions);
    }

    [HttpGet("{id:guid}/versions/{version:int}")]
    public async Task<ActionResult<EavFormTemplate>> GetByIdAndVersion(Guid id, int version)
    {
        if (version < 1)
            return BadRequest(new { Message = "Số phiên bản không hợp lệ." });

        var template = await _repository.GetByIdAndVersionAsync(id, version);
        if (template == null)
            return NotFound(new { Message = $"Không tìm thấy biểu mẫu ID = {id}, phiên bản {version}." });

        return Ok(template);
    }

    /// <summary>Khôi phục phiên bản — quyền EAV_COMPLETED_FORM_MANAGE.</summary>
    [HttpPut("{id:guid}/versions/{version:int}/restore")]
    public async Task<IActionResult> RestoreVersion(Guid id, int version)
    {
        if (version < 1)
            return BadRequest(new { Message = "Số phiên bản không hợp lệ." });

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Không tìm thấy form ID = {id}." });

        var target = await _repository.GetByIdAndVersionAsync(id, version);
        if (target == null)
            return NotFound(new { Message = $"Không tìm thấy phiên bản {version} của form." });
        if (target.IsActive)
            return BadRequest(new { Message = "Phiên bản này đang hoạt động, không cần khôi phục." });

        var ok = await _repository.RestoreVersionAsync(id, version);
        if (!ok)
            return NotFound(new { Message = $"Không tìm thấy phiên bản {version} của form." });

        return Ok(new { Message = $"Đã khôi phục form về phiên bản {version}." });
    }
}
