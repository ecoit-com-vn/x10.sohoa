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
[Route("api/catalog/document-type")]
public class DocumentTypeController : ControllerBase
{
    private readonly IDocumentTypeRepository _documentTypeRepository;

    public DocumentTypeController(IDocumentTypeRepository documentTypeRepository)
    {
        _documentTypeRepository = documentTypeRepository;
    }

    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> Lookup([FromQuery] string? keyword = null)
    {
        var (items, _) = await _documentTypeRepository.GetPagedAsync(1, 1000, keyword, 1);
        return Ok(items);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] int? status = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var (items, totalCount) = await _documentTypeRepository.GetPagedAsync(page, pageSize, keyword, status);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _documentTypeRepository.GetByIdAsync(id);
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DocumentType documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType.Code) || string.IsNullOrWhiteSpace(documentType.Name))
            return BadRequest(new { message = "Mã loại văn bản và Tên loại văn bản là bắt buộc." });

        var existing = await _documentTypeRepository.GetByCodeAsync(documentType.Code);
        if (existing != null)
            return BadRequest(new { message = $"Mã loại văn bản '{documentType.Code}' đã tồn tại trong hệ thống." });

        documentType.CreatedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        documentType.CreatedDate = DateTime.UtcNow;
        documentType.IsDeleted = false;

        var id = await _documentTypeRepository.CreateAsync(documentType);
        documentType.Id = id;

        return CreatedAtAction(nameof(GetById), new { id }, documentType);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DocumentType documentType)
    {
        if (id != documentType.Id)
            return BadRequest(new { message = "ID không trùng khớp." });

        if (string.IsNullOrWhiteSpace(documentType.Code) || string.IsNullOrWhiteSpace(documentType.Name))
            return BadRequest(new { message = "Mã loại văn bản và Tên loại văn bản là bắt buộc." });

        var existing = await _documentTypeRepository.GetByCodeAsync(documentType.Code);
        if (existing != null && existing.Id != id)
            return BadRequest(new { message = $"Mã loại văn bản '{documentType.Code}' đã được sử dụng bởi bản ghi khác." });

        var dbItem = await _documentTypeRepository.GetByIdAsync(id);
        if (dbItem == null)
            return NotFound();

        dbItem.Code = documentType.Code;
        dbItem.Name = documentType.Name;
        dbItem.FormId = documentType.FormId;
        dbItem.IsActive = documentType.IsActive;
        dbItem.IsEquipmentProfile = documentType.IsEquipmentProfile;
        dbItem.IsFactoryAcceptanceReport = documentType.IsFactoryAcceptanceReport;
        dbItem.IsCbmDocument = documentType.IsCbmDocument;
        dbItem.Piority = documentType.Piority;
        dbItem.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        dbItem.ModifiedDate = DateTime.UtcNow;

        var success = await _documentTypeRepository.UpdateAsync(dbItem);
        if (!success)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var dbItem = await _documentTypeRepository.GetByIdAsync(id);
        if (dbItem == null)
            return NotFound();

        var success = await _documentTypeRepository.DeleteAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id:guid}/lock")]
    public async Task<IActionResult> Lock(Guid id)
    {
        var dbItem = await _documentTypeRepository.GetByIdAsync(id);
        if (dbItem == null)
            return NotFound();

        dbItem.IsActive = false;
        dbItem.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        dbItem.ModifiedDate = DateTime.UtcNow;

        var success = await _documentTypeRepository.UpdateAsync(dbItem);
        if (!success)
            return StatusCode(500, new { message = "Không thể cập nhật trạng thái loại văn bản." });

        return Ok(new { message = "Đã khóa loại văn bản thành công.", status = 0 });
    }

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id)
    {
        var dbItem = await _documentTypeRepository.GetByIdAsync(id);
        if (dbItem == null)
            return NotFound();

        dbItem.IsActive = true;
        dbItem.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
        dbItem.ModifiedDate = DateTime.UtcNow;

        var success = await _documentTypeRepository.UpdateAsync(dbItem);
        if (!success)
            return StatusCode(500, new { message = "Không thể cập nhật trạng thái loại văn bản." });

        return Ok(new { message = "Đã mở khóa loại văn bản thành công.", status = 1 });
    }
}
