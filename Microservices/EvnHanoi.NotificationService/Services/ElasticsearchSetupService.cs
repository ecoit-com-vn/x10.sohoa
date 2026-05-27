using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.Analysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace EvnHanoi.NotificationService.Services;

public class ElasticsearchSetupService : IHostedService
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticsearchSetupService> _logger;

    public ElasticsearchSetupService(ElasticsearchClient client, ILogger<ElasticsearchSetupService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var indexName = "equipments";
        var existsResponse = await _client.Indices.ExistsAsync(indexName, cancellationToken);
        if (!existsResponse.Exists)
        {
            _logger.LogInformation("Creating index {IndexName} with custom analyzers...", indexName);

            var createResponse = await _client.Indices.CreateAsync(indexName, c => c
                .Settings(s => s
                    .Analysis(a => a
                        .TokenFilters(tf => tf
                            .Synonym("vn_synonym", sy => sy.Synonyms(new[] { "trạm, trạm biến áp", "máy, máy biến áp" })) // vn_synonym
                        )
                        .Analyzers(an => an
                            .Custom("vn_analyzer", ca => ca
                                .Tokenizer("standard") // Assuming standard tokenizer or custom vietnamese tokenizer
                                .Filter(new[] { "lowercase", "vn_synonym", "icu_folding" }) // vn_synonym trước icu_folding
                            )
                        )
                    )
                )
                .Mappings(m => m
                    .Properties(new Properties
                    {
                        { "id", new KeywordProperty() },
                        { "name", new TextProperty { Analyzer = "vn_analyzer" } },
                        { "code", new KeywordProperty() },
                        { "description", new TextProperty { Analyzer = "vn_analyzer" } },
                        { "type", new KeywordProperty() },
                        { "status", new KeywordProperty() }
                    })
                ), cancellationToken);

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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
