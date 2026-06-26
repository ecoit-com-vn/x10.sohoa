using System.Data;
using System.Security.Claims;
using Dapper;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Repositories;
using EvnHanoi.NotificationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.NotificationService.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/search")]
public class SearchController : ControllerBase
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly IDbConnection _dbConnection;
    private readonly IDossierSearchService _dossierSearchService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        ElasticsearchClient elasticClient,
        IDbConnection dbConnection,
        IDossierSearchService dossierSearchService,
        ILogger<SearchController> logger)
    {
        _elasticClient = elasticClient;
        _dbConnection = dbConnection;
        _dossierSearchService = dossierSearchService;
        _logger = logger;
    }

    [HttpGet("dossiers")]
    public async Task<IActionResult> SearchDossiers(
        [FromQuery] string? keyword,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] int? gridTypeId,
        [FromQuery] long? unitId,
        [FromQuery] string? tab,
        [FromQuery] string? status,
        [FromQuery] Guid? dossierTypeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var roles = GetUserRoles();
        var userId = GetUserId();
        var filter = new DossierFilterDto
        {
            Keyword = keyword,
            InfrastructureId = infrastructureId,
            GridTypeId = gridTypeId,
            UnitId = unitId,
            Tab = NormalizeTabParameter(tab, status),
            Status = status,
            DossierTypeId = dossierTypeId,
            UserId = userId,
            UserRoles = roles,
            IsAdmin = IsAdmin(roles),
            Page = page,
            PageSize = pageSize
        };

        var effectiveTab = DossierTabEsQuery.ResolveTabSlug(filter) ?? tab;
        if (string.Equals(effectiveTab, DossierListTabs.PendingAction, StringComparison.OrdinalIgnoreCase))
        {
            if (userId is null)
            {
                _logger.LogWarning(
                    "Dossier pending-action: JWT hợp lệ nhưng không có claim userId (GUID). Claim types: {ClaimTypes}",
                    string.Join(", ", User.Claims.Select(c => c.Type)));
            }
            else
            {
                var variants = DossierIndexIdNormalizer.GetGuidTermVariants(userId);
                _logger.LogInformation(
                    "Dossier pending-action: userId={UserId} variants=[{Variants}] roles={RoleCount}",
                    userId,
                    string.Join(", ", variants),
                    roles.Count);
            }
        }

        var (items, totalCount) = await _dossierSearchService.GetPagedAsync(filter);

        _logger.LogInformation(
            "Dossier search tab={Tab} userId={UserId} roles={RoleCount} page={Page} total={Total}",
            effectiveTab ?? "(none)",
            userId ?? "(null)",
            roles.Count,
            page,
            totalCount);

        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("dossiers/tab-counts")]
    public async Task<IActionResult> GetDossierTabCounts(
        [FromQuery] string? keyword,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] int? gridTypeId,
        [FromQuery] long? unitId)
    {
        var roles = GetUserRoles();
        var filter = new DossierFilterDto
        {
            Keyword = keyword,
            InfrastructureId = infrastructureId,
            GridTypeId = gridTypeId,
            UnitId = unitId,
            UserId = GetUserId(),
            UserRoles = roles,
            IsAdmin = IsAdmin(roles)
        };

        var counts = await _dossierSearchService.GetTabCountsAsync(filter);
        return Ok(counts);
    }

    /// <summary>
    /// [Development] So sánh JWT userId vs ES pendingAssigneeUserId — gọi khi tab Chờ xử lý trống mà ES có dữ liệu.
    /// </summary>
    [HttpGet("dossiers/diag/inbox")]
    public async Task<IActionResult> DiagnosePendingInbox([FromServices] IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
            return NotFound();

        var userId = GetUserId();
        var roles = GetUserRoles();
        var variants = userId is not null
            ? DossierIndexIdNormalizer.GetGuidTermVariants(userId).ToList()
            : new List<string>();

        var pendingFilter = new DossierFilterDto
        {
            Tab = DossierListTabs.PendingAction,
            UserId = userId,
            UserRoles = roles,
            IsAdmin = IsAdmin(roles),
            Page = 1,
            PageSize = 5
        };
        var (items, tabTotal) = await _dossierSearchService.GetPagedAsync(pendingFilter);

        long assigneeOnlyTotal = 0;
        if (variants.Count > 0)
        {
            var countResponse = await _elasticClient.CountAsync<DossierEsDocument>(c => c
                .Indices(DossierMessaging.IndexName)
                .Query(q => q.Bool(b =>
                {
                    b.MustNot(mn => mn.Term(t => t.Field(DossierEsFieldNames.IsDeleted).Value(true)));
                    b.Filter(f => f.Terms(t => t
                        .Field(DossierEsFieldNames.Status)
                        .Terms(new TermsQueryField(
                            DossierTabEsQuery.InPipelineStatuses.Select(FieldValue.String).ToArray()))));
                    b.Filter(f => f.Terms(t => t
                        .Field(DossierEsFieldNames.PendingAssigneeUserId)
                        .Terms(new TermsQueryField(variants.Select(FieldValue.String).ToArray()))));
                })));

            if (countResponse.IsValidResponse)
                assigneeOnlyTotal = countResponse.Count;
        }

        return Ok(new
        {
            elasticsearchNote = "Service dùng ES URL từ config Elasticsearch:Url hoặc Uri",
            userId,
            userIdVariants = variants,
            roles,
            pendingTabApiTotal = tabTotal,
            esAssigneeOnlyCount = assigneeOnlyTotal,
            sampleFromApi = items.Select(i => new
            {
                i.Id,
                i.Status,
                i.PendingAssigneeUserId
            }),
            jwtClaims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }

    private string? GetUserId() => JwtUserClaimResolver.ResolveUserId(User);

    private List<string> GetUserRoles() =>
        JwtUserClaimResolver.ResolveRoles(User).ToList();

    private static bool IsAdmin(IReadOnlyList<string> roles) =>
        roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase));

    [HttpGet("global")]
    public async Task<IActionResult> GlobalSearch([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(new
            {
                equipments = new List<object>(),
                dossiers = new List<object>(),
                eavformtemplates = new List<object>(),
                organizationUnits = new List<object>(),
                workflowDefinitions = new List<object>()
            });
        }

        var lowerQuery = query.ToLower();

        var eqResults = new List<object>();
        try
        {
            var eqResponse = await _elasticClient.SearchAsync<Equipment>(s => s
                .Index("equipments")
                .Query(q => q
                    .MultiMatch(mm => mm
                        .Query(query)
                        .Fields(new[] { "name", "description", "code" })
                    )
                )
                .Size(20)
            );
            if (eqResponse.IsValidResponse)
            {
                eqResults = eqResponse.Documents
                    .Select(e => new { id = e.Id, name = e.Name, link = $"/equipments/{e.Id}" })
                    .Cast<object>()
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching equipments in Elasticsearch.");
        }

        var dsResults = new List<object>();
        try
        {
            var dsResponse = await _elasticClient.SearchAsync<DossierEsDocument>(s => s
                .Indices(DossierMessaging.IndexName)
                .Size(20)
                .Query(q => { DossierSearchRepository.ConfigureQuery(q, new DossierFilterDto { Keyword = query }, query); })
            );
            if (dsResponse.IsValidResponse)
            {
                dsResults = dsResponse.Documents
                    .Select(d => new
                    {
                        id = d.Id,
                        name = BuildDossierDisplayName(d),
                        link = $"/dossiers/{d.Id}"
                    })
                    .Cast<object>()
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching dossiers in Elasticsearch.");
        }

        var tmplResults = new List<object>();
        try
        {
            var tmplResponse = await _elasticClient.SearchAsync<EavFormTemplate>(s => s
                .Index("eavformtemplates")
                .Query(q => q
                    .MultiMatch(mm => mm
                        .Query(query)
                        .Fields(new[] { "name", "description" })
                    )
                )
                .Size(20)
            );
            if (tmplResponse.IsValidResponse)
            {
                tmplResults = tmplResponse.Documents
                    .Select(t => new { id = t.Id, name = t.Name, link = $"/forms/{t.Id}" })
                    .Cast<object>()
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching eavformtemplates in Elasticsearch.");
        }

        var unitResults = new List<object>();
        try
        {
            if (_dbConnection.State != ConnectionState.Open) _dbConnection.Open();
            const string unitsSql = @"
                SELECT Id, Name 
                FROM ORGANIZATION_UNIT 
                WHERE LOWER(Code) LIKE :Query 
                   OR LOWER(Name) LIKE :Query 
                   OR LOWER(Description) LIKE :Query";

            var dbUnits = await _dbConnection.QueryAsync<(long Id, string Name)>(
                unitsSql,
                new { Query = $"%{lowerQuery}%" });

            unitResults = dbUnits
                .Select(u => new { id = u.Id.ToString(), name = u.Name, link = $"/organization-units/{u.Id}" })
                .Cast<object>()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching organization units in Oracle DB.");
        }

        var workflowResults = new List<object>();
        try
        {
            if (_dbConnection.State != ConnectionState.Open) _dbConnection.Open();
            const string workflowsSql = @"
                SELECT Id, Name 
                FROM WORKFLOWDEFINITIONS 
                WHERE LOWER(Name) LIKE :Query 
                   OR LOWER(Description) LIKE :Query";

            var dbWorkflows = await _dbConnection.QueryAsync<(string Id, string Name)>(
                workflowsSql,
                new { Query = $"%{lowerQuery}%" });

            workflowResults = dbWorkflows
                .Select(w => new { id = w.Id, name = w.Name, link = $"/workflows/{w.Id}" })
                .Cast<object>()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching workflow definitions in Oracle DB.");
        }

        return Ok(new
        {
            equipments = eqResults,
            dossiers = dsResults,
            eavformtemplates = tmplResults,
            organizationUnits = unitResults,
            workflowDefinitions = workflowResults
        });
    }

    private static string? NormalizeTabParameter(string? tab, string? status)
    {
        var resolved = DossierTabEsQuery.ResolveTabSlug(new DossierFilterDto
        {
            Tab = tab?.Trim(),
            Status = status?.Trim()
        });

        return resolved ?? tab?.Trim();
    }

    private static string BuildDossierDisplayName(DossierEsDocument doc)
    {
        if (!string.IsNullOrWhiteSpace(doc.InfrastructureName) && !string.IsNullOrWhiteSpace(doc.DossierTypeName))
            return $"{doc.InfrastructureName} — {doc.DossierTypeName}";
        if (!string.IsNullOrWhiteSpace(doc.InfrastructureName))
            return doc.InfrastructureName;
        if (!string.IsNullOrWhiteSpace(doc.DossierTypeName))
            return doc.DossierTypeName;
        return doc.Id;
    }
}
