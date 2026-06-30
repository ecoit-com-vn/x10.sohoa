using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.NotificationService.Controllers;

/// <summary>
/// Danh sách hồ sơ xuất bản (Elasticsearch) — route api/v1/search-publish.
/// Quyền: DOSSIER_PUBLISH_VIEW.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/search-publish")]
public class DossierPublishController : ControllerBase
{
    private readonly IDossierSearchService _dossierSearchService;
    private readonly IDossierMenuScopeValidator _menuScopeValidator;
    private readonly ILogger<DossierPublishController> _logger;

    public DossierPublishController(
        IDossierSearchService dossierSearchService,
        IDossierMenuScopeValidator menuScopeValidator,
        ILogger<DossierPublishController> logger)
    {
        _dossierSearchService = dossierSearchService;
        _menuScopeValidator = menuScopeValidator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] int? gridTypeId,
        [FromQuery] long? unitId,
        [FromQuery] string? tab,
        [FromQuery] Guid? dossierTypeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var roles = GetUserRoles();
        var userId = GetUserId();
        var isAdmin = IsAdmin(roles);
        var normalizedTab = NormalizeTabParameter(tab);

        var scopeCheck = await _menuScopeValidator.ValidateAsync(
            DossierMenuScopes.Publisher,
            normalizedTab,
            userId,
            Request.Headers.Authorization.ToString(),
            isAdmin);
        if (!scopeCheck.Allowed)
        {
            return StatusCode(403, new { message = scopeCheck.ErrorMessage });
        }

        var effectiveUnitId = unitId;
        var tokenUnitId = JwtUserClaimResolver.ResolveUnitId(User);
        if (tokenUnitId.HasValue)
        {
            effectiveUnitId = tokenUnitId.Value;
        }

        var filter = new DossierFilterDto
        {
            Keyword = keyword,
            InfrastructureId = infrastructureId,
            GridTypeId = gridTypeId,
            UnitId = effectiveUnitId,
            Tab = normalizedTab,
            MenuScope = DossierMenuScopes.Publisher,
            DossierTypeId = dossierTypeId,
            UserId = userId,
            UserRoles = roles,
            IsAdmin = isAdmin,
            Page = page,
            PageSize = pageSize
        };

        var (items, totalCount) = await _dossierSearchService.GetPagedAsync(filter);

        _logger.LogInformation(
            "Dossier publish list tab={Tab} userId={UserId} page={Page} total={Total}",
            normalizedTab ?? "(none)",
            userId ?? "(null)",
            page,
            totalCount);

        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("tab-counts")]
    public async Task<IActionResult> GetTabCounts(
        [FromQuery] string? keyword,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] int? gridTypeId,
        [FromQuery] long? unitId)
    {
        var roles = GetUserRoles();
        var userId = GetUserId();
        var isAdmin = IsAdmin(roles);

        var scopeCheck = await _menuScopeValidator.ValidateAsync(
            DossierMenuScopes.Publisher,
            tab: null,
            userId,
            Request.Headers.Authorization.ToString(),
            isAdmin);
        if (!scopeCheck.Allowed)
        {
            return StatusCode(403, new { message = scopeCheck.ErrorMessage });
        }

        var effectiveUnitId = unitId;
        var tokenUnitId = JwtUserClaimResolver.ResolveUnitId(User);
        if (tokenUnitId.HasValue)
        {
            effectiveUnitId = tokenUnitId.Value;
        }

        var filter = new DossierFilterDto
        {
            Keyword = keyword,
            InfrastructureId = infrastructureId,
            GridTypeId = gridTypeId,
            UnitId = effectiveUnitId,
            MenuScope = DossierMenuScopes.Publisher,
            UserId = userId,
            UserRoles = roles,
            IsAdmin = isAdmin
        };

        var counts = await _dossierSearchService.GetTabCountsAsync(filter);
        return Ok(counts);
    }

    private string? GetUserId() => JwtUserClaimResolver.ResolveUserId(User);

    private List<string> GetUserRoles() =>
        JwtUserClaimResolver.ResolveRoles(User).ToList();

    private static bool IsAdmin(IReadOnlyList<string> roles) =>
        roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeTabParameter(string? tab) =>
        DossierTabEsQuery.ResolveTabSlug(new DossierFilterDto { Tab = tab?.Trim() }) ?? tab?.Trim();
}
