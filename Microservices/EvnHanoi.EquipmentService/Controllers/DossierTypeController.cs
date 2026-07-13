using System;
using System.Security.Claims;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/catalog/dossier-type")]
public class DossierTypeController : ControllerBase
{
    private readonly IDossierTypeRepository _dossierTypeRepository;
    private readonly IDossierTypeService _dossierTypeService;

    public DossierTypeController(
        IDossierTypeRepository dossierTypeRepository,
        IDossierTypeService dossierTypeService)
    {
        _dossierTypeRepository = dossierTypeRepository;
        _dossierTypeService = dossierTypeService;
    }

    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> Lookup([FromQuery] string? keyword = null)
    {
        var (items, _) = await _dossierTypeRepository.GetPagedAsync(1, 1000, keyword, 1);
        return Ok(items);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] int? status = null)
    {
        // Default page and pageSize if not passed, in accordance with API guidelines
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var (items, totalCount) = await _dossierTypeRepository.GetPagedAsync(page, pageSize, keyword, status);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _dossierTypeRepository.GetByIdAsync(id);
        if (result == null) 
            return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DossierType dossierType)
    {
        if (string.IsNullOrWhiteSpace(dossierType.Code) || string.IsNullOrWhiteSpace(dossierType.Name))
            return BadRequest(new { message = "Mã loại hồ sơ và Tên loại hồ sơ là bắt buộc." });

        // Verify if Code already exists
        var existing = await _dossierTypeRepository.GetByCodeAsync(dossierType.Code);
        if (existing != null)
            return BadRequest(new { message = $"Mã loại hồ sơ '{dossierType.Code}' đã tồn tại trong hệ thống." });

        dossierType.CreatedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        dossierType.CreatedDate = DateTime.UtcNow;
        dossierType.IsDeleted = false;

        var id = await _dossierTypeRepository.CreateAsync(dossierType);
        dossierType.Id = id;

        return CreatedAtAction(nameof(GetById), new { id }, dossierType);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DossierType dossierType)
    {
        if (id != dossierType.Id)
            return BadRequest(new { message = "ID không trùng khớp." });

        if (string.IsNullOrWhiteSpace(dossierType.Code) || string.IsNullOrWhiteSpace(dossierType.Name))
            return BadRequest(new { message = "Mã loại hồ sơ và Tên loại hồ sơ là bắt buộc." });

        // Verify if Code already exists on another record
        var existing = await _dossierTypeRepository.GetByCodeAsync(dossierType.Code);
        if (existing != null && existing.Id != id)
            return BadRequest(new { message = $"Mã loại hồ sơ '{dossierType.Code}' đã được sử dụng bởi bản ghi khác." });

        var dbItem = await _dossierTypeRepository.GetByIdAsync(id);
        if (dbItem == null) 
            return NotFound();

        dbItem.Code = dossierType.Code;
        dbItem.Name = dossierType.Name;
        dbItem.FormId = dossierType.FormId;
        dbItem.IsActive = dossierType.IsActive;
        dbItem.DocumentTypeIds = dossierType.DocumentTypeIds;
        dbItem.Piority = dossierType.Piority;
        dbItem.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        dbItem.ModifiedDate = DateTime.UtcNow;

        var success = await _dossierTypeRepository.UpdateAsync(dbItem);
        if (!success) 
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var dbItem = await _dossierTypeRepository.GetByIdAsync(id);
        if (dbItem == null) 
            return NotFound();

        var success = await _dossierTypeRepository.DeleteAsync(id);
        if (!success) 
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id:guid}/lock")]
    public async Task<IActionResult> Lock(Guid id)
    {
        var dbItem = await _dossierTypeRepository.GetByIdAsync(id);
        if (dbItem == null) 
            return NotFound();

        dbItem.IsActive = false;
        dbItem.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        dbItem.ModifiedDate = DateTime.UtcNow;

        var success = await _dossierTypeRepository.UpdateAsync(dbItem);
        if (!success) 
            return StatusCode(500, new { message = "Không thể cập nhật trạng thái loại hồ sơ." });

        return Ok(new { message = "Đã khóa loại hồ sơ thành công.", status = 0 });
    }

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id)
    {
        var dbItem = await _dossierTypeRepository.GetByIdAsync(id);
        if (dbItem == null) 
            return NotFound();

        dbItem.IsActive = true;
        dbItem.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        dbItem.ModifiedDate = DateTime.UtcNow;

        var success = await _dossierTypeRepository.UpdateAsync(dbItem);
        if (!success) 
            return StatusCode(500, new { message = "Không thể cập nhật trạng thái loại hồ sơ." });

        return Ok(new { message = "Đã mở khóa loại hồ sơ thành công.", status = 1 });
    }

    [HttpPut("{id:guid}/update-eav")]
    public async Task<IActionResult> UpdateEav(Guid id, [FromBody] UpdateEavRequest request)
    {
        var updatedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        var result = await _dossierTypeService.UpdateEavAsync(
            id,
            request.FormId,
            request.Name,
            request.Code,
            request.Category,
            request.Description,
            request.DescriptionInfo,
            request.FormSchema,
            updatedBy
        );

        if (result == null)
            return BadRequest(new { message = "Không thể cập nhật cấu hình biểu mẫu EAV cho loại hồ sơ." });

        return Ok(result);
    }
}

public class UpdateEavRequest
{
    public Guid? FormId { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? DescriptionInfo { get; set; }
    public string? FormSchema { get; set; }
}
