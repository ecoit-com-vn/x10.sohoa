using EvnHanoi.Infrastructure.Security;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.NotificationService.Controllers;

/// <summary>
/// Tra cứu toàn văn tài liệu OCR (Elasticsearch document_index).
/// Quyền: DOCUMENT_FULLTEXT_SEARCH_VIEW.
/// </summary>
[Authorize]
[ApiController]
[BypassDynamicPermission]
[Route("api/v1/search/documents")]
public class DocumentSearchController : ControllerBase
{
    private readonly IDocumentSearchService _documentSearchService;
    private readonly IDossierMenuScopeValidator _menuScopeValidator;
    private readonly ILogger<DocumentSearchController> _logger;

    public DocumentSearchController(
        IDocumentSearchService documentSearchService,
        IDossierMenuScopeValidator menuScopeValidator,
        ILogger<DocumentSearchController> logger)
    {
        _documentSearchService = documentSearchService;
        _menuScopeValidator = menuScopeValidator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? keyword,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var roles = GetUserRoles();
        var userId = GetUserId();
        var isAdmin = IsAdmin(roles);

        var scopeCheck = await _menuScopeValidator.ValidateAsync(
            DossierMenuScopes.DocumentFulltext,
            tab: null,
            userId,
            Request.Headers.Authorization.ToString(),
            isAdmin);
        if (!scopeCheck.Allowed)
            return StatusCode(403, new { message = scopeCheck.ErrorMessage });

        var filter = new DocumentSearchFilterDto
        {
            Keyword = keyword,
            Sort = string.IsNullOrWhiteSpace(sort) ? "newest" : sort.Trim(),
            UnitId = isAdmin ? null : JwtUserClaimResolver.ResolveUnitId(User),
            IsAdmin = isAdmin,
            Page = page,
            PageSize = pageSize
        };

        var (items, totalCount) = await _documentSearchService.SearchAsync(filter);

        _logger.LogInformation(
            "Document fulltext search userId={UserId} keyword={Keyword} page={Page} total={Total}",
            userId ?? "(null)",
            keyword ?? "(none)",
            page,
            totalCount);

        return Ok(new
        {
            items,
            totalCount,
            page,
            pageSize,
            keyword
        });
    }

    [HttpGet("{versionId}")]
    public async Task<IActionResult> GetDetail(string versionId)
    {
        if (string.IsNullOrWhiteSpace(versionId))
            return BadRequest(new { message = "DocumentVersionId không hợp lệ." });

        var roles = GetUserRoles();
        var userId = GetUserId();
        var isAdmin = IsAdmin(roles);

        var scopeCheck = await _menuScopeValidator.ValidateAsync(
            DossierMenuScopes.DocumentFulltext,
            tab: null,
            userId,
            Request.Headers.Authorization.ToString(),
            isAdmin);
        if (!scopeCheck.Allowed)
            return StatusCode(403, new { message = scopeCheck.ErrorMessage });

        var scope = new DocumentSearchFilterDto
        {
            UnitId = isAdmin ? null : JwtUserClaimResolver.ResolveUnitId(User),
            IsAdmin = isAdmin
        };

        var detail = await _documentSearchService.GetDetailAsync(versionId.Trim(), scope);
        if (detail is null)
            return NotFound(new { message = "Không tìm thấy tài liệu hoặc bạn không có quyền xem." });

        return Ok(detail);
    }

    private string? GetUserId() => JwtUserClaimResolver.ResolveUserId(User);

    private List<string> GetUserRoles() =>
        JwtUserClaimResolver.ResolveRoles(User).ToList();

    private static bool IsAdmin(IReadOnlyList<string> roles) =>
        roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase));
}
