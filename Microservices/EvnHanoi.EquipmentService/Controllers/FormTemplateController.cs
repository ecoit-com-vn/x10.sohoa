using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

[ApiController]
[Route("api/v1/form-templates")]
public class FormTemplateController : ControllerBase
{
    private readonly IEavFormTemplateRepository _repository;
    private readonly IEavFormTemplateService _service;

    public FormTemplateController(
        IEavFormTemplateRepository repository,
        IEavFormTemplateService service)
    {
        _repository = repository;
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetAllActive()
    {
        var templates = await _repository.GetAllActiveAsync("TEMPLATE", null);
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
                "TEMPLATE",
                request.GridTypeId,
                request.ExtractionProcess
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
                "TEMPLATE",
                request.GridTypeId,
                request.ExtractionProcess);

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

        if (!string.IsNullOrEmpty(existing.Code))
        {
            var versions = await _repository.GetVersionsByCodeAsync(existing.Code);
            foreach (var version in versions)
            {
                version.IsDeleted = true;
                await _repository.UpdateAsync(version);
            }
        }
        else
        {
            existing.IsDeleted = true;
            await _repository.UpdateAsync(existing);
        }

        return NoContent();
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

    [HttpGet("code/{code}/versions")]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetVersionsByCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest("Mã biểu mẫu không được để trống.");

        var versions = await _repository.GetVersionsByCodeAsync(code);
        return Ok(versions);
    }
}
