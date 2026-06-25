using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using EvnHanoi.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.NotificationService.Services;

public static class DossierIndexSetup
{
    public static async Task EnsureIndexExistsAsync(
        ElasticsearchClient client,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var indexName = DossierMessaging.IndexName;
        var existsResponse = await client.Indices.ExistsAsync(indexName, cancellationToken);
        if (existsResponse.Exists)
        {
            logger.LogInformation("Index {IndexName} already exists.", indexName);
            await EnsureMappingUpdatedAsync(client, logger, cancellationToken);
            return;
        }

        logger.LogInformation("Creating index {IndexName}...", indexName);

        // vi_tokenizer (elasticsearch-analysis-vietnamese) + asciifolding.
        var createResponse = await client.Indices.CreateAsync(indexName, c => c
            .Settings(s => s
                .Analysis(VietnameseAnalysisSetup.Configure)
            )
            .Mappings(m => m.Properties(BuildDossierIndexProperties()))
        , cancellationToken);

        if (createResponse.IsValidResponse)
        {
            logger.LogInformation("Index {IndexName} created successfully.", indexName);
            return;
        }

        logger.LogWarning(
            "Failed to create {IndexName} with vi_tokenizer, retrying with standard tokenizer + asciifolding. Error: {Error}",
            indexName,
            createResponse.DebugInformation);

        var fallbackResponse = await client.Indices.CreateAsync(indexName, c => c
            .Settings(s => s
                .Analysis(VietnameseAnalysisSetup.ConfigureStandardTokenizer)
            )
            .Mappings(m => m.Properties(BuildDossierIndexProperties()))
        , cancellationToken);

        if (fallbackResponse.IsValidResponse)
            logger.LogInformation("Index {IndexName} created with standard tokenizer + asciifolding.", indexName);
        else
            logger.LogError(
                "Failed to create index {IndexName}: {Error}",
                indexName,
                fallbackResponse.DebugInformation);
    }

    private static Properties BuildDossierIndexProperties(bool useStandardAnalyzer = false)
    {
        TextProperty TextField(bool searchable = false)
        {
            if (useStandardAnalyzer)
                return new TextProperty();

            var prop = new TextProperty { Analyzer = VietnameseAnalysisSetup.AnalyzerName };
            if (searchable)
                prop.SearchAnalyzer = VietnameseAnalysisSetup.SearchAnalyzerName;
            prop.Fields = new Properties { { "keyword", new KeywordProperty() } };
            return prop;
        }

        var textField = TextField();
        var searchableText = TextField(searchable: true);

        return new Properties
        {
            { "id", new KeywordProperty() },
            { "gridTypeId", new IntegerNumberProperty() },
            { "gridTypeName", new KeywordProperty() },
            { "infrastructureId", new KeywordProperty() },
            { "infrastructureName", searchableText },
            { "infrastructureCode", new KeywordProperty() },
            { "unitId", new LongNumberProperty() },
            { "dossierSetId", new KeywordProperty() },
            { "dossierSetName", textField },
            { "dossierTypeId", new KeywordProperty() },
            { "dossierTypeName", textField },
            { "status", new KeywordProperty() },
            { "workflowStatusName", new KeywordProperty() },
            { "workflowInstanceId", new KeywordProperty() },
            { "workflowInstanceStatus", new KeywordProperty() },
            { "creatorId", new KeywordProperty() },
            { "creatorUsername", new KeywordProperty() },
            { "creatorName", textField },
            { "createdDate", new DateProperty() },
            { "modifiedDate", new DateProperty() },
            { "documentCount", new IntegerNumberProperty() },
            { "pendingAssignedRoles", new KeywordProperty() },
            { "pendingAssigneeUserId", new KeywordProperty() },
            { "workflowParticipantUserIds", new KeywordProperty() },
            { "currentStepAllowEdit", new BooleanProperty() },
            { "currentVersionNumber", new IntegerNumberProperty() },
            { "isDeleted", new BooleanProperty() },
            {
                "catalogFields", new NestedProperty
                {
                    Properties = new Properties
                    {
                        { "catalogCode", new KeywordProperty() },
                        { "catalogName", new KeywordProperty() },
                        { "sortOrder", new IntegerNumberProperty() },
                        { "value", searchableText }
                    }
                }
            },
            {
                "formFields", new NestedProperty
                {
                    Properties = new Properties
                    {
                        { "fieldCode", new KeywordProperty() },
                        { "textValue", searchableText },
                        { "numericValue", new DoubleNumberProperty() },
                        { "dateValue", new DateProperty() }
                    }
                }
            },
            {
                "equipments", new NestedProperty
                {
                    Properties = new Properties
                    {
                        { "equipmentId", new KeywordProperty() },
                        { "equipmentCode", new KeywordProperty() },
                        { "equipmentName", textField },
                        { "serialNumber", new KeywordProperty() }
                    }
                }
            }
        };
    }

    private static async Task EnsureMappingUpdatedAsync(
        ElasticsearchClient client,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var indexName = DossierMessaging.IndexName;
        var response = await client.Indices.PutMappingAsync(indexName, m => m
            .Properties(new Properties
            {
                { "pendingAssignedRoles", new KeywordProperty() },
                { "pendingAssigneeUserId", new KeywordProperty() },
                { "workflowParticipantUserIds", new KeywordProperty() },
                { "currentStepAllowEdit", new BooleanProperty() },
                { "workflowInstanceStatus", new KeywordProperty() },
            }), cancellationToken);

        if (response.IsValidResponse)
            logger.LogInformation("Updated mapping for {IndexName} (pending inbox fields).", indexName);
        else
            logger.LogWarning(
                "Could not update mapping for {IndexName}: {Error}",
                indexName,
                response.DebugInformation);
    }
}
