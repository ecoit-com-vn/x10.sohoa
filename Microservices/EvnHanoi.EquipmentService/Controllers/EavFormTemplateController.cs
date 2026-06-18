using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

using EvnHanoi.Infrastructure.Security;

namespace EvnHanoi.EquipmentService.Controllers;

[ApiController]
[Route("api/v1/eav-form-templates")]
public class EavFormTemplateController : ControllerBase
{
    private readonly IEavFormTemplateRepository _repository;
    private readonly IEavFormTemplateService _service;

    public EavFormTemplateController(
        IEavFormTemplateRepository repository,
        IEavFormTemplateService service)
    {
        _repository = repository;
        _service = service;
    }

    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> Lookup()
    {
        var templates = await _repository.GetAllActiveAsync();
        return Ok(templates);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetAllActive()
    {
        var templates = await _repository.GetAllActiveAsync("FORM");
        return Ok(templates);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EavFormTemplate>> GetById(Guid id)
    {
        var template = await _repository.GetByIdAsync(id);
        if (template == null)
            return NotFound(new { Message = $"Không tìm thấy biểu mẫu với ID = {id}" });

        return Ok(template);
    }

    /// <summary>
    /// Lấy biểu mẫu EAV để gen trường nhập liệu trên màn hình tạo/sửa hồ sơ.
    /// Bypass DynamicPermission để màn hình tạo hồ sơ luôn truy cập được.
    /// </summary>
    [HttpGet("{id:guid}/get-form")]
    [BypassDynamicPermission]
    public async Task<ActionResult<EavFormTemplate>> GetFormForInput(Guid id)
    {
        var template = await _repository.GetByIdAsync(id);
        if (template == null)
            return NotFound(new { Message = $"Không tìm thấy biểu mẫu với ID = {id}" });

        return Ok(template);
    }

    [HttpPost]
    public async Task<ActionResult<EavFormTemplate>> Create([FromBody] CreateEavFormTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.FormSchema) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest("Tên biểu mẫu, mã biểu mẫu, hạng mục áp dụng và cấu trúc Schema không được để trống.");
        }

        try
        {
            var template = await _service.CreateFormTemplateAsync(
                request.Name,
                request.Code,
                request.Category,
                request.Description ?? string.Empty,
                request.DescriptionInfo ?? string.Empty,
                request.FormSchema,
                request.CreatedBy ?? "admin",
                request.EquipmentTypeId,
                "FORM",
                request.GridTypeId
            );

            return CreatedAtAction(nameof(GetById), new { id = template.Id.ToString() }, template);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = $"Lỗi máy chủ khi tạo biểu mẫu: {ex.Message}" });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EavFormTemplate>> UpgradeVersion(Guid id, [FromBody] UpgradeEavFormTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.FormSchema) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest("Tên biểu mẫu, mã biểu mẫu, hạng mục áp dụng và cấu trúc Schema không được để trống.");
        }

        try
        {
            var updatedBy = request.UpdatedBy ?? "admin";
            var newTemplate = await _service.UpdateFormTemplateAsync(
                id, 
                request.Name, 
                request.Code,
                request.Category,
                request.Description ?? string.Empty, 
                request.DescriptionInfo ?? string.Empty, 
                request.FormSchema, 
                updatedBy,
                request.EquipmentTypeId,
                "FORM",
                request.GridTypeId);

            return Ok(newTemplate);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Inactivate(Guid id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Không tìm thấy biểu mẫu với ID = {id}" });

        existing.IsActive = false;
        await _repository.UpdateAsync(existing);
        return NoContent();
    }

    [HttpPut("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Không tìm thấy biểu mẫu với ID = {id}" });

        if (existing.Status != "Tạo mới" && existing.Status != "Từ chối" && !string.IsNullOrEmpty(existing.Status))
        {
            return BadRequest(new { Message = "Chỉ biểu mẫu ở trạng thái 'Tạo mới' hoặc 'Từ chối' mới được gửi duyệt." });
        }

        existing.Status = "Chờ duyệt";
        await _repository.UpdateAsync(existing);
        return Ok(existing);
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
        return Ok(existing);
    }
}

public class CreateEavFormTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionInfo { get; set; }
    public string FormSchema { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public Guid? EquipmentTypeId { get; set; }
    public int? GridTypeId { get; set; }
}

public class UpgradeEavFormTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionInfo { get; set; }
    public string FormSchema { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public Guid? EquipmentTypeId { get; set; }
    public int? GridTypeId { get; set; }
}
