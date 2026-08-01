using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

public partial class EquipmentController
{
    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst(ClaimTypes.Name)?.Value
        ?? User.Identity?.Name
        ?? "system";

    /// <summary>OCR / bóc tách tài liệu hồ sơ liên quan thiết bị — quyền EQUIPMENT_EDIT.</summary>
    [HttpPost("{equipmentId:guid}/dossiers/{dossierId:guid}/documents/{versionId:guid}/digitization")]
    public async Task<IActionResult> StartEquipmentDocumentDigitization(
        Guid equipmentId,
        Guid dossierId,
        Guid versionId,
        [FromBody] SubmitDossierDocumentDigitizationRequest? request)
    {
        var accessError = await ValidateEquipmentDossierContextAsync(equipmentId, dossierId);
        if (accessError != null)
            return accessError;

        var equipmentDto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        if (string.IsNullOrWhiteSpace(equipmentDto?.FormSchema))
            return BadRequest(new { message = "Loại thiết bị chưa có biểu mẫu thông số kỹ thuật." });

        request ??= new SubmitDossierDocumentDigitizationRequest();
        request.FormSchemaJson = equipmentDto.FormSchema;

        try
        {
            var result = await _documentDigitizationService.SubmitForDossierDocumentAsync(
                dossierId,
                versionId,
                request,
                UserId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = ex.Message });
        }
    }

    /// <summary>Bóc tách lại tài liệu hồ sơ liên quan thiết bị — quyền EQUIPMENT_EDIT.</summary>
    [HttpPost("{equipmentId:guid}/dossiers/{dossierId:guid}/documents/{versionId:guid}/digitization/rerun-extraction")]
    public async Task<IActionResult> RerunEquipmentDocumentExtraction(
        Guid equipmentId,
        Guid dossierId,
        Guid versionId)
    {
        var accessError = await ValidateEquipmentDossierContextAsync(equipmentId, dossierId);
        if (accessError != null)
            return accessError;

        var equipmentDto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        if (string.IsNullOrWhiteSpace(equipmentDto?.FormSchema))
            return BadRequest(new { message = "Loại thiết bị chưa có biểu mẫu thông số kỹ thuật." });

        try
        {
            var result = await _documentDigitizationService.ReExtractForDossierDocumentAsync(
                dossierId,
                versionId,
                UserId,
                equipmentDto.FormSchema);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<IActionResult?> ValidateEquipmentDossierContextAsync(Guid equipmentId, Guid dossierId)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        if (dto == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value)))
            return Forbid();

        var linkedEquipments = await _dossierRepository.GetEquipmentsAsync(dossierId);
        if (!linkedEquipments.Any(e => e.EquipmentId == equipmentId))
            return BadRequest(new { message = "Hồ sơ không liên kết với thiết bị này." });

        return null;
    }

    /// <summary>Lấy danh sách tài liệu lý lịch thiết bị kỹ thuật EAV/OCR — quyền EQUIPMENT_VIEW.</summary>
    [HttpGet("{equipmentId:guid}/profile-documents")]
    public async Task<IActionResult> GetProfileDocuments(
        Guid equipmentId,
        [FromQuery] DossierDocumentFilterDto filter)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        if (dto == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value)))
            return Forbid();

        try
        {
            var (items, totalCount) = await _documentRepository.GetProfileDocumentsByEquipmentAsync(equipmentId, filter);
            return Ok(new { items, totalCount, page = filter.Page, pageSize = filter.PageSize });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Danh sách tài liệu lý lịch chỉ thuộc hồ sơ đã xuất bản.</summary>
    [HttpGet("{equipmentId:guid}/published-profile-documents")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetPublishedProfileDocuments(
        Guid equipmentId,
        [FromQuery] DossierDocumentFilterDto filter)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        if (dto == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value)))
            return Forbid();

        try
        {
            var (items, totalCount) = await _documentRepository.GetPublishedProfileDocumentsByEquipmentAsync(equipmentId, filter);
            return Ok(new { items, totalCount, page = filter.Page, pageSize = filter.PageSize });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Tạo download token tài liệu lý lịch thiết bị (thuộc hồ sơ liên quan) — quyền EQUIPMENT_VIEW.</summary>
    [HttpGet("{equipmentId:guid}/documents/{versionId:guid}/download-url")]
    public async Task<IActionResult> GetProfileDocumentDownloadUrl(
        Guid equipmentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        if (dto == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value)))
            return Forbid();

        if (!await _documentRepository.IsPublishedEquipmentProfileDocumentVersionForEquipmentAsync(equipmentId, versionId))
            return NotFound(new { message = "Tài liệu lý lịch không thuộc thiết bị này hoặc không tồn tại." });

        var version = await _documentRepository.GetDocumentVersionByIdAsync(versionId);
        if (version == null || string.IsNullOrEmpty(version.FilePath))
            return NotFound(new { message = "Phiên bản tài liệu không tồn tại hoặc chưa có file." });

        var document = await _documentRepository.GetDocumentByIdAsync(version.DocumentId);
        if (document == null)
            return NotFound(new { message = "Tài liệu không tồn tại." });

        try
        {
            var result = await _downloadTokenService.CreateTokenAsync(
                version.FilePath,
                document.Name,
                version.MimeType ?? "application/octet-stream",
                _fileStorageService.DossierBucketName,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>OCR + Bóc tách tài liệu lý lịch theo biểu mẫu thiết bị — quyền EQUIPMENT_EDIT.</summary>
    [HttpPost("{equipmentId:guid}/documents/{versionId:guid}/digitization")]
    public async Task<IActionResult> StartEquipmentDocumentDigitizationOnly(
        Guid equipmentId,
        Guid versionId)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        if (dto == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value)))
            return Forbid();

        if (string.IsNullOrWhiteSpace(dto.FormSchema))
            return BadRequest(new { message = "Loại thiết bị chưa gắn biểu mẫu thông số kỹ thuật." });

        try
        {
            var result = await _documentDigitizationService.SubmitForEquipmentDocumentAsync(
                equipmentId,
                versionId,
                UserId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Bóc tách lại tài liệu lý lịch theo biểu mẫu thiết bị — quyền EQUIPMENT_EDIT.</summary>
    [HttpPost("{equipmentId:guid}/documents/{versionId:guid}/digitization/rerun-extraction")]
    public async Task<IActionResult> RerunEquipmentProfileDocumentExtraction(
        Guid equipmentId,
        Guid versionId)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        if (dto == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value)))
            return Forbid();

        //if (string.IsNullOrWhiteSpace(dto.FormSchema))
        //    return BadRequest(new { message = "Loại thiết bị chưa gắn biểu mẫu thông số kỹ thuật." });

        try
        {
            var result = await _documentDigitizationService.ReExtractForEquipmentDocumentAsync(
                equipmentId,
                versionId,
                UserId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Lấy kết quả bóc tách của tài liệu theo thiết bị — quyền EQUIPMENT_VIEW.</summary>
    [HttpGet("{equipmentId:guid}/documents/{versionId:guid}/digitization/result")]
    public async Task<IActionResult> GetEquipmentDocumentDigitizationResult(
        Guid equipmentId,
        Guid versionId)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        if (dto == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value)))
            return Forbid();

        try
        {
            var result = await _documentDigitizationService.GetExtractionResultForEquipmentAsync(equipmentId, versionId);
            if (result == null)
                return NotFound(new { message = "Chưa có kết quả bóc tách cho tài liệu này." });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Lưu kết quả bóc tách đã chỉnh sửa; tùy chọn thay FormValues thiết bị — quyền EQUIPMENT_EDIT.</summary>
    [HttpPut("{equipmentId:guid}/documents/{versionId:guid}/digitization/result")]
    public async Task<IActionResult> SaveEquipmentDocumentDigitizationResult(
        Guid equipmentId,
        Guid versionId,
        [FromBody] SaveDocumentExtractionDataRequest request)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
        if (dto == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value)))
            return Forbid();

        try
        {
            var result = await _documentDigitizationService.SaveEquipmentExtractionDataAsync(
                equipmentId,
                versionId,
                request,
                UserId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
