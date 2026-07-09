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
[Route("api/v1/eav-completed-forms")]
public class EavCompletedFormController : ControllerBase
{
    private readonly IEavFormTemplateRepository _repository;

    public EavCompletedFormController(IEavFormTemplateRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetAllActive()
    {
        var templates = await _repository.GetCompletedFormsAsync();
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

    [HttpGet("code/{code}/versions")]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetVersionsByCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest("Mã biểu mẫu không được để trống.");

        var versions = await _repository.GetVersionsByCodeAsync(code);
        return Ok(versions);
    }
}
