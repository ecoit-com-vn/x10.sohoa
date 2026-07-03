using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using EvnHanoi.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.NotificationService.Services;

public static class DocumentIndexSetup
{
    public static async Task EnsureIndexExistsAsync(
        ElasticsearchClient client,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var indexName = DocumentTextMessaging.IndexName;
        var existsResponse = await client.Indices.ExistsAsync(indexName, cancellationToken);
        if (existsResponse.Exists)
        {
            logger.LogInformation("Index {IndexName} already exists.", indexName);
            return;
        }

        logger.LogInformation("Creating index {IndexName}...", indexName);

        var createResponse = await client.Indices.CreateAsync(indexName, c => c
            .Settings(s => s.Analysis(VietnameseAnalysisSetup.Configure))
            .Mappings(m => m.Properties(BuildDocumentIndexProperties()))
        , cancellationToken);

        if (createResponse.IsValidResponse)
        {
            logger.LogInformation("Index {IndexName} created successfully.", indexName);
            return;
        }

        logger.LogWarning(
            "Failed to create {IndexName} with vi_tokenizer, retrying with standard tokenizer. Error: {Error}",
            indexName,
            createResponse.DebugInformation);

        var fallbackResponse = await client.Indices.CreateAsync(indexName, c => c
            .Settings(s => s.Analysis(VietnameseAnalysisSetup.ConfigureStandardTokenizer))
            .Mappings(m => m.Properties(BuildDocumentIndexProperties()))
        , cancellationToken);

        if (fallbackResponse.IsValidResponse)
            logger.LogInformation("Index {IndexName} created with standard tokenizer.", indexName);
        else
            logger.LogError(
                "Failed to create index {IndexName}: {Error}",
                indexName,
                fallbackResponse.DebugInformation);
    }

    private static Properties BuildDocumentIndexProperties()
    {
        var searchableText = new TextProperty
        {
            Analyzer = VietnameseAnalysisSetup.AnalyzerName,
            SearchAnalyzer = VietnameseAnalysisSetup.SearchAnalyzerName,
            Fields = new Properties { { "keyword", new KeywordProperty() } }
        };

        var textField = new TextProperty
        {
            Analyzer = VietnameseAnalysisSetup.AnalyzerName,
            Fields = new Properties { { "keyword", new KeywordProperty() } }
        };

        return new Properties
        {
            { "id", new KeywordProperty() },
            { "documentId", new KeywordProperty() },
            { "documentVersionId", new KeywordProperty() },
            { "documentName", searchableText },
            { "fullText", searchableText },
            { "mimeType", new KeywordProperty() },
            { "filePath", new KeywordProperty() },
            { "bucketName", new KeywordProperty() },
            { "dossierId", new KeywordProperty() },
            { "dossierTitle", textField },
            { "infrastructureId", new KeywordProperty() },
            { "infrastructureName", searchableText },
            { "infrastructureCode", new KeywordProperty() },
            { "unitId", new LongNumberProperty() },
            { "dossierTypeId", new KeywordProperty() },
            { "dossierTypeName", textField },
            { "documentTypeId", new KeywordProperty() },
            { "documentTypeName", textField },
            { "statusId", new IntegerNumberProperty() },
            { "statusCode", new KeywordProperty() },
            { "publishStatusId", new IntegerNumberProperty() },
            { "publishStatusCode", new KeywordProperty() },
            { "equipmentNames", textField },
            { "extractionSummary", textField },
            { "ocrCompletedAt", new DateProperty() },
            { "indexedAt", new DateProperty() },
            { "isDeleted", new BooleanProperty() }
        };
    }
}
