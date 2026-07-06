using EvnHanoi.Infrastructure.Security;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Services;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.NotificationService.Controllers;

/// <summary>
/// Tìm kiếm hồ sơ đã xuất bản cho báo cáo — gọi nội bộ từ ReportService.
/// </summary>
[ApiController]
[Route("internal/v1/report-dossiers")]
[BypassDynamicPermission]
public class InternalReportDossierSearchController : ControllerBase
{
    private readonly IDossierSearchService _dossierSearchService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalReportDossierSearchController> _logger;

    public InternalReportDossierSearchController(
        IDossierSearchService dossierSearchService,
        IConfiguration configuration,
        ILogger<InternalReportDossierSearchController> logger)
    {
        _dossierSearchService = dossierSearchService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromQuery] long? unitId,
        [FromQuery] bool isAdmin,
        [FromQuery] int? gridTypeId,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] int? infrastructureTypeId,
        [FromQuery] Guid? equipmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError))
            return tokenError!;

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var filter = new DossierFilterDto
        {
            UnitId = unitId,
            IsAdmin = isAdmin,
            GridTypeId = gridTypeId,
            InfrastructureId = infrastructureId,
            InfrastructureTypeId = infrastructureTypeId,
            EquipmentId = equipmentId,
            Tab = DossierListTabs.Published,
            MenuScope = DossierMenuScopes.Report,
            Page = page,
            PageSize = pageSize
        };

        var (items, totalCount) = await _dossierSearchService.GetPagedAsync(filter);

        _logger.LogInformation(
            "Internal report dossier search isAdmin={IsAdmin} unitId={UnitId} page={Page} total={Total}",
            isAdmin,
            unitId,
            page,
            totalCount);

        return Ok(new { items, totalCount, page, pageSize });
    }

    private bool ValidateInternalToken(string? internalToken, out IActionResult? errorResult)
    {
        errorResult = null;
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
        {
            errorResult = StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên NotificationService." });
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
