using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

[ApiController]
[Route("internal/v1/published-dossiers")]
[BypassDynamicPermission]
public class InternalPublishedDossierDocumentsController : ControllerBase
{
    private readonly IDossierService _dossierService;
    private readonly IDossierDocumentService _dossierDocumentService;
    private readonly IConfiguration _configuration;

    public InternalPublishedDossierDocumentsController(
        IDossierService dossierService,
        IDossierDocumentService dossierDocumentService,
        IConfiguration configuration)
    {
        _dossierService = dossierService;
        _dossierDocumentService = dossierDocumentService;
        _configuration = configuration;
    }

    [HttpGet("{id:guid}/documents")]
    public async Task<IActionResult> GetDocuments(
        Guid id,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromQuery] bool isAdmin = false,
        [FromQuery] long? unitId = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!ValidateToken(internalToken, out var error)) return error!;

        if (!await _dossierService.IsPublishedDossierAccessibleAsync(id, isAdmin, unitId))
            return NotFound(new { message = "Không tìm thấy hồ sơ đã xuất bản hoặc không có quyền truy cập." });

        var filter = new DossierDocumentFilterDto { Keyword = keyword, Page = page, PageSize = pageSize };
        var (items, totalCount) = await _dossierDocumentService.GetDocumentsAsync(id, filter);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id:guid}/documents/{versionId:guid}/download-url")]
    public async Task<IActionResult> GetDownloadUrl(
        Guid id,
        Guid versionId,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromQuery] bool isAdmin = false,
        [FromQuery] long? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateToken(internalToken, out var error)) return error!;

        if (!await _dossierService.IsPublishedDossierAccessibleAsync(id, isAdmin, unitId))
            return NotFound(new { message = "Không tìm thấy hồ sơ đã xuất bản hoặc không có quyền truy cập." });

        var result = await _dossierDocumentService.GetDownloadTokenAsync(id, versionId, cancellationToken);
        return Ok(result);
    }

    private bool ValidateToken(string? internalToken, out IActionResult? errorResult)
    {
        errorResult = null;
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
        {
            errorResult = StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên EquipmentService." });
            return false;
        }

        if (!string.Equals(internalToken, expected, StringComparison.Ordinal))
        {
            errorResult = Unauthorized(new { message = "Token nội bộ không hợp lệ." });
            return false;
        }

        return true;
    }
}
