using DocumentFormat.OpenXml.Spreadsheet;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EvnHanoi.EquipmentService.Controllers;

[ApiController]
[Route("api/v1/eav-form-templates")]
public class EavFormTemplateController : ControllerBase
{
    private readonly IEavFormTemplateRepository _repository;
    private readonly IEavFormTemplateService _service;
    private readonly ICatalogRepository _catalogRepository;

    public EavFormTemplateController(
        IEavFormTemplateRepository repository,
        IEavFormTemplateService service,
        ICatalogRepository catalogRepository)
    {
        _repository = repository;
        _service = service;
        _catalogRepository = catalogRepository;
    }

    /// <summary>Lookup biểu mẫu FORM trạng thái Hoàn thành và đang hoạt động (không trả FormSchema).</summary>
    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> Lookup()
    {
        var templates = await _repository.GetCompletedActiveFormsAsync();
        return Ok(templates);
    }

    [HttpGet("design")]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetDesignList(
        [FromQuery] string? keyword,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? status)
    {

        if (startDate.HasValue && endDate.HasValue && startDate.Value.Date > endDate.Value.Date)
            return BadRequest(new { message = "Từ ngày không được lớn hơn Đến ngày." });

        var filter = new EavFormTemplateFilterDto
        {
            Keyword = keyword,
            StartDate = startDate,
            EndDate = endDate,
            Status = status
        };

        var (items, totalCount) = await _repository.GetDesignFormsAsync(filter);
        return Ok(new { items, totalCount});
    }

    [HttpGet("approval")]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetApprovalList()
    {
        var templates = await _repository.GetApprovalFormsAsync();
        return Ok(templates);
    }

    [HttpGet("completed")]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetCompletedList()
    {
        var templates = await _repository.GetCompletedFormsAsync();
        return Ok(templates);
    }

    /// <summary>Lookup danh mục phục vụ thiết kế form — quyền EAV_FORM_TEMPLATE_VIEW</summary>
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

    /// <summary>Lookup hạng mục HMAD cho dropdown tạo/sửa form</summary>
    [HttpGet("hmad-categories")]
    public async Task<ActionResult<IEnumerable<object>>> GetHmadCategories()
    {
        var catalogType = await _catalogRepository.GetCatalogTypeByCodeAsync("HMAD");
        if (catalogType == null) return Ok(Array.Empty<object>());
        var items = await _catalogRepository.GetAllAsync(catalogTypeId: catalogType.Id, status: 1);
        return Ok(items.Select(c => new { c.Id, c.Code, c.Name }));
    }

    /// <summary>Lookup loại danh mục cho builder form</summary>
    [HttpGet("catalog-types")]
    public async Task<ActionResult<IEnumerable<object>>> GetCatalogTypesLookup()
    {
        var types = await _catalogRepository.GetCatalogTypesAsync();
        return Ok(types.Select(t => new { t.Id, t.Code, t.Name }));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetAllActive()
    {
        var templates = await _repository.GetAllActiveAsync("FORM", null);
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

    [HttpGet("by-equipment-type/{equipmentTypeId:guid}")]
    [BypassDynamicPermission]
    public async Task<ActionResult<EavFormTemplate>> GetActiveTemplateByEquipmentType(Guid equipmentTypeId)
    {
        var matched = await _repository.GetActiveByEquipmentTypeIdAsync(equipmentTypeId);
        if (matched == null)
            return NotFound(new { Message = "Không tìm thấy biểu mẫu hoạt động cho loại thiết bị này." });

        return Ok(matched);
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
                request.GridTypeId,
                request.ExtractionProcess,
                request.ExtractionPosition
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
                request.GridTypeId,
                request.ExtractionProcess,
                request.ExtractionPosition);

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

        existing.IsDeleted = true;
        await _repository.UpdateAsync(existing);
        await _repository.DeleteVersionsAsync(existing.Id);

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



    [HttpGet("code/{code}/versions")]
    public async Task<ActionResult<IEnumerable<EavFormTemplate>>> GetVersionsByCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest("Mã biểu mẫu không được để trống.");

        var versions = await _repository.GetVersionsByCodeAsync(code);
        return Ok(versions);
    }

    [HttpPost("versions/{versionId:guid}/activate")]
    public async Task<IActionResult> ActivateVersion(Guid versionId)
    {
        try
        {
            await _repository.ActivateVersionAsync(versionId);
            return Ok(new { Message = "Kích hoạt phiên bản thành công!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = $"Lỗi khi kích hoạt phiên bản: {ex.Message}" });
        }
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

    /// <summary>Khôi phục phiên bản — chỉ áp dụng cho version đang ngưng; đặt IsActive=1, các version khác = 0.</summary>
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

public class CreateEavFormTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionInfo { get; set; }
    public string? ExtractionProcess { get; set; }
    public string? ExtractionPosition { get; set; }
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
    public string? ExtractionProcess { get; set; }
    public string? ExtractionPosition { get; set; }
    public string FormSchema { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public Guid? EquipmentTypeId { get; set; }
    public int? GridTypeId { get; set; }
} 
