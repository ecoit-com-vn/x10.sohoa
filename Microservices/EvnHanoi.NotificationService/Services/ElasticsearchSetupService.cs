using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.Analysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Services;

public class ElasticsearchSetupService : IHostedService
{
    private readonly ElasticsearchClient _client;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ElasticsearchSetupService> _logger;

    public ElasticsearchSetupService(
        ElasticsearchClient client, 
        IServiceProvider serviceProvider, 
        ILogger<ElasticsearchSetupService> logger)
    {
        _client = client;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 1. Create indices
            await EnsureIndexExistsAsync("equipments", new Properties
            {
                { "id", new KeywordProperty() },
                { "name", new TextProperty { Analyzer = "vn_analyzer" } },
                { "code", new KeywordProperty() },
                { "description", new TextProperty { Analyzer = "vn_analyzer" } },
                { "type", new KeywordProperty() },
                { "status", new KeywordProperty() }
            }, cancellationToken);

            await EnsureIndexExistsAsync("dossiers", new Properties
            {
                { "id", new KeywordProperty() },
                { "equipmentId", new KeywordProperty() },
                { "title", new TextProperty { Analyzer = "vn_analyzer" } },
                { "description", new TextProperty { Analyzer = "vn_analyzer" } },
                { "status", new KeywordProperty() },
                { "publishStatus", new KeywordProperty() },
                { "unitId", new LongNumberProperty() }
            }, cancellationToken);

            await EnsureIndexExistsAsync("eavformtemplates", new Properties
            {
                { "id", new KeywordProperty() },
                { "name", new TextProperty { Analyzer = "vn_analyzer" } },
                { "description", new TextProperty { Analyzer = "vn_analyzer" } },
                { "version", new IntegerNumberProperty() },
                { "isActive", new BooleanProperty() }
            }, cancellationToken);

            // 2. Sync existing database records
            await SyncDatabaseToElasticsearchAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during Elasticsearch initialization/sync.");
        }
    }

    private async Task EnsureIndexExistsAsync(string indexName, Properties properties, CancellationToken cancellationToken)
    {
        var existsResponse = await _client.Indices.ExistsAsync(indexName, cancellationToken);
        if (!existsResponse.Exists)
        {
            _logger.LogInformation("Creating index {IndexName} with custom analyzers...", indexName);

            var createResponse = await _client.Indices.CreateAsync(indexName, c => c
                .Settings(s => s
                    .Analysis(a => a
                        .TokenFilters(tf => tf
                            .Synonym("vn_synonym", sy => sy.Synonyms(new[] { "trạm, trạm biến áp", "máy, máy biến áp" }))
                        )
                        .Analyzers(an => an
                            .Custom("vn_analyzer", ca => ca
                                .Tokenizer("standard")
                                .Filter(new[] { "lowercase", "vn_synonym", "icu_folding" })
                            )
                        )
                    )
                )
                .Mappings(m => m.Properties(properties))
            , cancellationToken);

            if (createResponse.IsValidResponse)
            {
                _logger.LogInformation("Index {IndexName} created successfully.", indexName);
            }
            else
            {
                _logger.LogError("Failed to create index {IndexName}: {Error}", indexName, createResponse.DebugInformation);
            }
        }
        else
        {
            _logger.LogInformation("Index {IndexName} already exists.", indexName);
        }
    }

    private async Task SyncDatabaseToElasticsearchAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting database synchronization to Elasticsearch...");
        using var scope = _serviceProvider.CreateScope();
        var dbConnection = scope.ServiceProvider.GetRequiredService<IDbConnection>();
        
        if (dbConnection.State != ConnectionState.Open)
        {
            dbConnection.Open();
        }

        // Sync Equipments
        try
        {
            var equipments = await dbConnection.QueryAsync<Equipment>(
                "SELECT Id, Name, Code, '' AS Description, SerialNumber AS Type, '1' AS Status FROM Equipments");
            _logger.LogInformation("Syncing {Count} equipments to Elasticsearch...", equipments.Count());
            foreach (var eq in equipments)
            {
                if (cancellationToken.IsCancellationRequested) break;
                await _client.IndexAsync(eq, idx => idx.Index("equipments").Id(eq.Id), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing Equipments table.");
        }

        // Sync Dossiers
        try
        {
            var dossiers = await dbConnection.QueryAsync<Dossier>(
                "SELECT Id, EquipmentId, Title, Description, Status, Status AS PublishStatus, UnitId FROM Dossiers");
            _logger.LogInformation("Syncing {Count} dossiers to Elasticsearch...", dossiers.Count());
            foreach (var ds in dossiers)
            {
                if (cancellationToken.IsCancellationRequested) break;
                await _client.IndexAsync(ds, idx => idx.Index("dossiers").Id(ds.Id), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing Dossiers table.");
        }

        // Sync EavFormTemplates
        try
        {
            var templates = await dbConnection.QueryAsync<EavFormTemplate>(
                "SELECT Id, Name, Description, Version, IsActive FROM EavFormTemplates");
            _logger.LogInformation("Syncing {Count} templates to Elasticsearch...", templates.Count());
            foreach (var tmpl in templates)
            {
                if (cancellationToken.IsCancellationRequested) break;
                await _client.IndexAsync(tmpl, idx => idx.Index("eavformtemplates").Id(tmpl.Id), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing EavFormTemplates table.");
        }

        _logger.LogInformation("Database synchronization to Elasticsearch completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
