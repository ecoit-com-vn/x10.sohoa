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

    public EavFormApprovalController(IEavFormTemplateRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetAllActive()
    {
        var templates = await _repository.GetApprovalFormsAsync();
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
}
