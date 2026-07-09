using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.NotificationService.Controllers;

/// <summary>
/// Danh sách tra cứu hồ sơ thiết bị (Elasticsearch) — route api/v1/search-dossiers-by-equipment.
/// Chỉ hồ sơ đã duyệt và đã xuất bản, lọc theo đơn vị đăng nhập + đơn vị con.
/// Quyền: SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/search-dossiers-by-equipment")]
public class SearchDossiersByEquipmentController : ControllerBase
{
    private readonly IDossierSearchService _dossierSearchService;
    private readonly IDossierMenuScopeValidator _menuScopeValidator;
    private readonly ILogger<SearchDossiersByEquipmentController> _logger;

    public SearchDossiersByEquipmentController(
        IDossierSearchService dossierSearchService,
        IDossierMenuScopeValidator menuScopeValidator,
        ILogger<SearchDossiersByEquipmentController> logger)
    {
        _dossierSearchService = dossierSearchService;
        _menuScopeValidator = menuScopeValidator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] DateTime? publishDateFrom,
        [FromQuery] DateTime? publishDateTo,
        [FromQuery] int? gridTypeId,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] Guid? equipmentTypeId,
        [FromQuery] Guid? equipmentId,
        [FromQuery] Guid? dossierTypeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var roles = GetUserRoles();
        var userId = GetUserId();
        var isAdmin = IsAdmin(roles);

        var scopeCheck = await _menuScopeValidator.ValidateAsync(
            DossierMenuScopes.EquipmentLookup,
            tab: DossierListTabs.Published,
            userId,
            Request.Headers.Authorization.ToString(),
            isAdmin);
        if (!scopeCheck.Allowed)
            return StatusCode(403, new { message = scopeCheck.ErrorMessage });

        long? effectiveUnitId = null;
        if (!isAdmin)
        {
            effectiveUnitId = JwtUserClaimResolver.ResolveUnitId(User);
            if (!effectiveUnitId.HasValue)
                return Unauthorized(new { message = "Không thể xác định đơn vị của người dùng" });
        }

        var filter = new DossierFilterDto
        {
            Keyword = keyword,
            PublishDateFrom = publishDateFrom,
            PublishDateTo = publishDateTo,
            GridTypeId = gridTypeId,
            InfrastructureId = infrastructureId,
            EquipmentTypeId = equipmentTypeId,
            EquipmentId = equipmentId,
            DossierTypeId = dossierTypeId,
            UnitId = effectiveUnitId,
            Tab = DossierListTabs.Published,
            MenuScope = DossierMenuScopes.EquipmentLookup,
            UserId = userId,
            UserRoles = roles,
            IsAdmin = isAdmin,
            Page = page,
            PageSize = pageSize
        };

        var (items, totalCount) = await _dossierSearchService.GetPagedAsync(filter);

        _logger.LogInformation(
            "Dossier equipment lookup userId={UserId} page={Page} total={Total}",
            userId ?? "(null)",
            page,
            totalCount);

        return Ok(new { items, totalCount, page, pageSize });
    }

    private string? GetUserId() => JwtUserClaimResolver.ResolveUserId(User);

    private List<string> GetUserRoles() =>
        JwtUserClaimResolver.ResolveRoles(User).ToList();

    private static bool IsAdmin(IReadOnlyList<string> roles) =>
        roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase));
}
