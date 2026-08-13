using EvnHanoi.Infrastructure.Security;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.NotificationService.Controllers;

[Authorize]
[ApiController]
[BypassDynamicPermission]
[Route("api/v1/search/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDossierSearchService _dossierSearchService;
    private readonly IAuditLogService _auditLogService;

    public DashboardController(
        IDossierSearchService dossierSearchService,
        IAuditLogService auditLogService)
    {
        _dossierSearchService = dossierSearchService;
        _auditLogService = auditLogService;
    }

    [HttpGet("dossiers")]
    public async Task<IActionResult> GetRecentDossiers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5)
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (!await _auditLogService.CheckPermissionAsync(authHeader, User, "VIEW_DASHBOARD"))
            return StatusCode(403, new { message = "Không có quyền truy cập Dashboard." });

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var roles = JwtUserClaimResolver.ResolveRoles(User).ToList();
        var isAdmin = roles.Any(role => role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase));
        var filter = new DossierFilterDto
        {
            UserId = JwtUserClaimResolver.ResolveUserId(User),
            UserRoles = roles,
            IsAdmin = isAdmin,
            UnitId = isAdmin ? null : JwtUserClaimResolver.ResolveUnitId(User),
            Page = page,
            PageSize = pageSize
        };

        var (items, totalCount) = await _dossierSearchService.GetPagedAsync(filter);
        return Ok(new { items, totalCount, page, pageSize });
    }
}
