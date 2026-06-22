using System.Data;
using Dapper;
using Elastic.Clients.Elasticsearch;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Repositories;
using EvnHanoi.NotificationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        [FromQuery] string? status,
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10)
    {
        var filter = new DossierFilterDto
        {
            Keyword = keyword,
            InfrastructureId = infrastructureId,
            GridTypeId = gridTypeId,
            UnitId = unitId,
            Status = status,
            Page = page,
            PageSize = pageSize
        };

        var (items, totalCount) = await _dossierSearchService.GetPagedAsync(filter);
        return Ok(new { items, totalCount, page, pageSize });
    }

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
