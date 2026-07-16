using System.Data;
using Dapper;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.Analysis;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                { "name", new TextProperty { Analyzer = VietnameseAnalysisSetup.AnalyzerName, SearchAnalyzer = VietnameseAnalysisSetup.SearchAnalyzerName } },
                { "code", new KeywordProperty() },
                { "description", new TextProperty { Analyzer = VietnameseAnalysisSetup.AnalyzerName, SearchAnalyzer = VietnameseAnalysisSetup.SearchAnalyzerName } },
                { "type", new KeywordProperty() },
                { "status", new KeywordProperty() }
            }, cancellationToken);

            await DossierIndexSetup.EnsureIndexExistsAsync(_client, _logger, cancellationToken);
            await DocumentIndexSetup.EnsureIndexExistsAsync(_client, _logger, cancellationToken);

            await EnsureIndexExistsAsync("eavformtemplates", new Properties
            {
                { "id", new KeywordProperty() },
                { "name", new TextProperty { Analyzer = VietnameseAnalysisSetup.AnalyzerName, SearchAnalyzer = VietnameseAnalysisSetup.SearchAnalyzerName } },
                { "description", new TextProperty { Analyzer = VietnameseAnalysisSetup.AnalyzerName, SearchAnalyzer = VietnameseAnalysisSetup.SearchAnalyzerName } },
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
                    .Analysis(VietnameseAnalysisSetup.Configure)
                )
                .Mappings(m => m.Properties(properties))
            , cancellationToken);

            if (createResponse.IsValidResponse)
            {
                _logger.LogInformation("Index {IndexName} created successfully.", indexName);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to create index {IndexName} with vi_tokenizer, retrying with standard tokenizer + asciifolding. Error: {Error}",
                    indexName,
                    createResponse.DebugInformation);

                var fallbackResponse = await _client.Indices.CreateAsync(indexName, c => c
                    .Settings(s => s
                        .Analysis(VietnameseAnalysisSetup.ConfigureStandardTokenizer)
                    )
                    .Mappings(m => m.Properties(properties))
                , cancellationToken);

                if (fallbackResponse.IsValidResponse)
                    _logger.LogInformation("Index {IndexName} created with standard tokenizer + asciifolding.", indexName);
                else
                    _logger.LogError("Failed to create index {IndexName}: {Error}", indexName, fallbackResponse.DebugInformation);
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

        // Dossier: chỉ bootstrap khi index rỗng (fresh deploy). Cập nhật hàng ngày qua RabbitMQ / reindex nội bộ.
        try
        {
            await BootstrapDossierIndexIfEmptyAsync(scope, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bootstrapping dossier_index.");
        }

        // Document fulltext: bootstrap published OCR docs khi document_index rỗng.
        try
        {
            await BootstrapDocumentIndexIfEmptyAsync(scope, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bootstrapping document_index.");
        }

        // Sync EavFormTemplates
        try
        {
            var templates = await dbConnection.QueryAsync<EavFormTemplate>(@"
                SELECT t.Id, v.Name as Name, v.Description as Description, v.Version as Version, 
                       t.IsActive as IsActive, t.GridTypeId, t.ExtractionProcess 
                FROM EavFormTemplates t
                LEFT JOIN EavFormTemplateVersions v ON t.Id = v.FormTemplateId AND v.IsActive = 1 AND v.IsDeleted = 0 AND v.Version = (
                    SELECT MAX(Version) FROM EavFormTemplateVersions WHERE FormTemplateId = t.Id AND IsActive = 1 AND IsDeleted = 0
                )
                WHERE t.IsDeleted = 0");
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

    private async Task BootstrapDossierIndexIfEmptyAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        var countResponse = await _client.CountAsync<DossierEsDocument>(
            c => c.Indices(DossierMessaging.IndexName),
            cancellationToken);

        if (!countResponse.IsValidResponse)
        {
            _logger.LogWarning(
                "Could not count {IndexName} — skipping dossier bootstrap. Error: {Error}",
                DossierMessaging.IndexName,
                countResponse.DebugInformation);
            return;
        }

        if (countResponse.Count > 0)
        {
            _logger.LogInformation(
                "{IndexName} already has {Count} document(s) — skipping startup dossier sync (use RabbitMQ or internal reindex).",
                DossierMessaging.IndexName,
                countResponse.Count);
            return;
        }

        var enrichmentRepository = scope.ServiceProvider.GetRequiredService<IDossierEnrichmentRepository>();
        var indexer = scope.ServiceProvider.GetRequiredService<IDossierIndexer>();

        var deletedIds = (await enrichmentRepository.GetSoftDeletedIdsAsync()).ToList();
        if (deletedIds.Count > 0)
        {
            _logger.LogInformation(
                "Purging {Count} soft-deleted dossiers from empty {IndexName} bootstrap...",
                deletedIds.Count,
                DossierMessaging.IndexName);
            foreach (var dossierId in deletedIds)
            {
                if (cancellationToken.IsCancellationRequested) break;
                await indexer.DeleteByIdAsync(dossierId, cancellationToken);
            }
        }

        var dossierIds = (await enrichmentRepository.GetAllIdsAsync()).ToList();
        _logger.LogInformation(
            "Bootstrapping {Count} active dossiers into empty {IndexName}...",
            dossierIds.Count,
            DossierMessaging.IndexName);
        foreach (var dossierId in dossierIds)
        {
            if (cancellationToken.IsCancellationRequested) break;
            await indexer.IndexByIdAsync(dossierId, cancellationToken);
        }
    }

    private async Task BootstrapDocumentIndexIfEmptyAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        var countResponse = await _client.CountAsync<DocumentEsDocument>(
            c => c.Indices(DocumentTextMessaging.IndexName),
            cancellationToken);

        if (!countResponse.IsValidResponse)
        {
            _logger.LogWarning(
                "Could not count {IndexName} — skipping document bootstrap. Error: {Error}",
                DocumentTextMessaging.IndexName,
                countResponse.DebugInformation);
            return;
        }

        if (countResponse.Count > 0)
        {
            _logger.LogInformation(
                "{IndexName} already has {Count} document(s) — skipping startup document sync (use RabbitMQ or POST internal/v1/documents/reindex-published).",
                DocumentTextMessaging.IndexName,
                countResponse.Count);
            return;
        }

        var enrichmentRepository = scope.ServiceProvider.GetRequiredService<IDocumentEnrichmentRepository>();
        var indexer = scope.ServiceProvider.GetRequiredService<IDocumentIndexer>();

        var versionIds = (await enrichmentRepository.GetPublishedIndexableVersionIdsAsync()).ToList();
        _logger.LogInformation(
            "Bootstrapping {Count} published OCR document version(s) into empty {IndexName}...",
            versionIds.Count,
            DocumentTextMessaging.IndexName);

        var indexed = 0;
        var failed = 0;
        foreach (var versionId in versionIds)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var ok = await indexer.IndexByVersionIdAsync(versionId, null, null, 0, cancellationToken);
            if (ok)
                indexed++;
            else
                failed++;
        }

        _logger.LogInformation(
            "Document bootstrap completed for {IndexName}: total={Total}, indexed={Indexed}, failed={Failed}.",
            DocumentTextMessaging.IndexName,
            versionIds.Count,
            indexed,
            failed);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
