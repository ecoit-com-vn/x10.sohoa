using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Dapper;
using Elastic.Clients.Elasticsearch;
using EvnHanoi.NotificationService.Models;
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
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        ElasticsearchClient elasticClient,
        IDbConnection dbConnection,
        ILogger<SearchController> logger)
    {
        _elasticClient = elasticClient;
        _dbConnection = dbConnection;
        _logger = logger;
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

        // 1. Query Elasticsearch for Equipments
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

        // 2. Query Elasticsearch for Dossiers
        var dsResults = new List<object>();
        try
        {
            var dsResponse = await _elasticClient.SearchAsync<Dossier>(s => s
                .Index("dossiers")
                .Query(q => q
                    .MultiMatch(mm => mm
                        .Query(query)
                        .Fields(new[] { "title", "description" })
                    )
                )
                .Size(20)
            );
            if (dsResponse.IsValidResponse)
            {
                dsResults = dsResponse.Documents
                    .Select(d => new { id = d.Id, name = d.Title, link = $"/dossiers/{d.Id}" })
                    .Cast<object>()
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching dossiers in Elasticsearch.");
        }

        // 3. Query Elasticsearch for Templates
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

        // 4. Query Oracle Database for Organization Units
        var unitResults = new List<object>();
        try
        {
            if (_dbConnection.State != ConnectionState.Open) _dbConnection.Open();
            var unitsSql = @"
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

        // 5. Query Oracle Database for Workflow Definitions
        var workflowResults = new List<object>();
        try
        {
            if (_dbConnection.State != ConnectionState.Open) _dbConnection.Open();
            var workflowsSql = @"
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
}
