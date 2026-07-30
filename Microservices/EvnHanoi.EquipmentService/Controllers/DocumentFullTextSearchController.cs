using System.Security.Claims;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Tra cứu toàn văn tài liệu OCR — phân quyền DOCUMENT_FULLTEXT_SEARCH_VIEW.
/// Proxy Elasticsearch qua NotificationService; dữ liệu hồ sơ/tài liệu đọc từ Oracle (published scope).
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/document-fulltext-search")]
public class DocumentFullTextSearchController : ControllerBase
{
    private readonly IDocumentFulltextSearchNotificationClient _notificationClient;
    private readonly IDossierService _dossierService;
    private readonly IDossierDocumentService _dossierDocumentService;
    private readonly IDocumentTypeRepository _documentTypeRepository;
    private readonly IDossierTypeRepository _dossierTypeRepository;
    private readonly IEavFormTemplateRepository _eavFormTemplateRepository;

    public DocumentFullTextSearchController(
        IDocumentFulltextSearchNotificationClient notificationClient,
        IDossierService dossierService,
        IDossierDocumentService dossierDocumentService,
        IDocumentTypeRepository documentTypeRepository,
        IDossierTypeRepository dossierTypeRepository,
        IEavFormTemplateRepository eavFormTemplateRepository)
    {
        _notificationClient = notificationClient;
        _dossierService = dossierService;
        _dossierDocumentService = dossierDocumentService;
        _documentTypeRepository = documentTypeRepository;
        _dossierTypeRepository = dossierTypeRepository;
        _eavFormTemplateRepository = eavFormTemplateRepository;
    }

    [HttpGet("documents")]
    public async Task<IActionResult> SearchDocuments(CancellationToken cancellationToken)
    {
        var response = await _notificationClient.SearchDocumentsAsync(Request.QueryString.Value?.TrimStart('?'), cancellationToken);
        return await ToProxyResultAsync(response);
    }

    [HttpGet("documents/{versionId}")]
    public async Task<IActionResult> GetDocumentDetail(string versionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(versionId))
            return BadRequest(new { message = "DocumentVersionId không hợp lệ." });

        var response = await _notificationClient.GetDocumentDetailAsync(versionId.Trim(), cancellationToken);
        return await ToProxyResultAsync(response);
    }

    [HttpGet("dossiers/{id:guid}")]
    public async Task<IActionResult> GetDossierDetail(Guid id)
    {
        var (isAdmin, unitId) = ResolveUserScope();
        if (!isAdmin && unitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var detail = await _dossierService.GetPublishedDetailByIdAsync(id, isAdmin, unitId);
        if (detail is null)
            return NotFound(new { message = $"Không tìm thấy hồ sơ đã xuất bản với ID = {id}" });

        return Ok(detail);
    }

    [HttpGet("dossiers/{id:guid}/related")]
    public async Task<IActionResult> GetRelatedDossiers(
        Guid id,
        [FromQuery] string? keyword,
        [FromQuery] Guid? dossierTypeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (isAdmin, unitId) = ResolveUserScope();
        if (!isAdmin && unitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        var dossier = await _dossierService.GetPublishedDetailByIdAsync(id, isAdmin, unitId);
        if (dossier is null)
            return NotFound(new { message = $"Không tìm thấy hồ sơ đã xuất bản với ID = {id}" });

        var scopedUnitId = isAdmin ? null : unitId;
        var (items, totalCount) = await _dossierService.GetCatalogDossiersAsync(
            keyword, dossier.InfrastructureId, dossierTypeId, scopedUnitId, page, pageSize);

        // Loại bỏ hồ sơ hiện tại khỏi danh sách hồ sơ liên quan để tránh tự hiển thị chính nó
        var filteredItems = items.Where(item => item.Id != id).ToList();
        var count = totalCount;
        if (items.Count() != filteredItems.Count)
        {
            count = Math.Max(0, totalCount - 1);
        }

        return Ok(new { items = filteredItems, totalCount = count, page, pageSize });
    }

    [HttpGet("dossiers/{id:guid}/documents")]
    public async Task<IActionResult> GetDocuments(
        Guid id,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var access = await EnsurePublishedDossierAccessAsync(id);
        if (access is not null)
            return access;

        try
        {
            var filter = new DossierDocumentFilterDto
            {
                Keyword = keyword,
                Page = page,
                PageSize = pageSize
            };
            var (items, totalCount) = await _dossierDocumentService.GetDocumentsAsync(id, filter);
            return Ok(new { items, totalCount, page, pageSize });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("dossiers/{id:guid}/documents/{versionId:guid}/download-url")]
    public async Task<IActionResult> GetDocumentDownloadUrl(
        Guid id,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var access = await EnsurePublishedDossierAccessAsync(id);
        if (access is not null)
            return access;

        try
        {
            var result = await _dossierDocumentService.GetDownloadTokenAsync(id, versionId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = ex.Message });
        }
    }

    [HttpGet("dossiers/{id:guid}/documents/{versionId:guid}/form-template")]
    public async Task<IActionResult> GetDocumentFormTemplate(Guid id, Guid versionId)
    {
        var access = await EnsurePublishedDossierAccessAsync(id);
        if (access is not null)
            return access;

        try
        {
            var template = await _dossierDocumentService.GetFormTemplateForDocumentVersionAsync(id, versionId);
            if (template is null)
                return NotFound(new { message = "Không tìm thấy biểu mẫu EAV cho tài liệu này." });

            return Ok(template);
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

    [HttpGet("document-types/lookup")]
    public async Task<IActionResult> LookupDocumentTypes([FromQuery] string? keyword = null)
    {
        var (items, _) = await _documentTypeRepository.GetPagedAsync(1, 1000, keyword, 1);
        return Ok(items);
    }

    [HttpGet("dossier-types/lookup")]
    public async Task<IActionResult> LookupDossierTypes([FromQuery] string? keyword = null)
    {
        var (items, _) = await _dossierTypeRepository.GetPagedAsync(1, 1000, keyword, 1);
        return Ok(items);
    }

    [HttpGet("form-templates/{formId:guid}/get-form")]
    public async Task<IActionResult> GetFormTemplate(Guid formId)
    {
        var template = await _eavFormTemplateRepository.GetByIdAsync(formId);
        if (template is null)
            return NotFound(new { message = $"Không tìm thấy biểu mẫu với ID = {formId}" });

        return Ok(template);
    }

    private async Task<IActionResult?> EnsurePublishedDossierAccessAsync(Guid dossierId)
    {
        var (isAdmin, unitId) = ResolveUserScope();
        if (!isAdmin && unitId is null)
            return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });

        if (!await _dossierService.IsPublishedDossierAccessibleAsync(dossierId, isAdmin, unitId))
            return NotFound(new { message = $"Không tìm thấy hồ sơ đã xuất bản với ID = {dossierId}" });

        return null;
    }

    private (bool IsAdmin, long? UnitId) ResolveUserScope()
    {
        var isAdmin = User.IsInRole("ADMIN") ||
                      User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");

        long? unitId = null;
        if (!isAdmin)
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            if (long.TryParse(unitIdClaim, out var userUnitId) && userUnitId > 0)
                unitId = userUnitId;
        }

        return (isAdmin, unitId);
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = body,
            ContentType = contentType
        };
    }
}
