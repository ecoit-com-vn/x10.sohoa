using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using EvnHanoi.Infrastructure.Security;

namespace EvnHanoi.EquipmentService.Controllers;

[ApiController]
[Route("api/v1/eav-form-approvals")]
public class EavFormApprovalController : ControllerBase
{
    private readonly IEavFormTemplateRepository _repository;
    private readonly ICatalogRepository _catalogRepository;

    public EavFormApprovalController(
        IEavFormTemplateRepository repository,
        ICatalogRepository catalogRepository)
    {
        _repository = repository;
        _catalogRepository = catalogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetAllActive()
    {
        var templates = await _repository.GetApprovalFormsAsync();
        return Ok(templates);
    }

    /// <summary>
    /// Lookup tên danh mục phục vụ preview form phê duyệt — tránh gọi CatalogController (quyền khác).
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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EavFormTemplate>> GetById(Guid id)
    {
        var template = await _repository.GetByIdAsync(id);
        if (template == null)
            return NotFound(new { Message = $"Không tìm thấy biểu mẫu với ID = {id}" });

        return Ok(template);
    }

    [HttpPut("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Không tìm thấy biểu mẫu với ID = {id}" });

        if (existing.Status != "Chờ duyệt")
        {
            return BadRequest(new { Message = "Chỉ biểu mẫu ở trạng thái 'Chờ duyệt' mới được phê duyệt." });
        }

        existing.Status = "Hoàn thành";
        await _repository.UpdateAsync(existing);
        await _repository.ApproveVersionAsync(id, "Hoàn thành");
        return Ok(existing);
    }

    [HttpPut("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Không tìm thấy biểu mẫu với ID = {id}" });

        if (existing.Status != "Chờ duyệt")
        {
            return BadRequest(new { Message = "Chỉ biểu mẫu ở trạng thái 'Chờ duyệt' mới được từ chối." });
        }

        existing.Status = "Từ chối";
        await _repository.UpdateAsync(existing);
        await _repository.ApproveVersionAsync(id, "Từ chối");
        return Ok(existing);
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

    /// <summary>Khôi phục phiên bản — quyền EAV_FORM_APPROVAL_APPROVE.</summary>
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
