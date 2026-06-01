using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Nest;

namespace EvnHanoi.EquipmentService.Infrastructure.Services;

public class ElasticsearchService : IElasticsearchService
{
    private readonly IElasticClient _elasticClient;
    private const string IndexName = "equipment_index";

    public ElasticsearchService(IConfiguration configuration)
    {
        var url = configuration["Elasticsearch:Url"] ?? "http://localhost:9200";
        var settings = new ConnectionSettings(new Uri(url))
            .DefaultIndex(IndexName);

        _elasticClient = new ElasticClient(settings);
    }

    public async Task CreateIndexAsync()
    {
        var existsResponse = await _elasticClient.Indices.ExistsAsync(IndexName);
        if (!existsResponse.Exists)
        {
            var createIndexResponse = await _elasticClient.Indices.CreateAsync(IndexName, c => c
                .Settings(s => s
                    .Analysis(a => a
                        .Analyzers(an => an
                            .Custom("vietnamese_analyzer", ca => ca
                                .Tokenizer("vi_tokenizer")
                                .Filters("lowercase")
                            )
                        )
                    )
                )
                .Map<Equipment>(m => m
                    .Properties(p => p
                        .Text(t => t
                            .Name(n => n.Name)
                            .Analyzer("vietnamese_analyzer")
                        )
                        .Text(t => t
                            .Name(n => n.Code)
                            .Analyzer("vietnamese_analyzer")
                        )
                        .Text(t => t
                            .Name(n => n.SerialNumber)
                            .Analyzer("vietnamese_analyzer")
                        )
                    )
                )
            );

            if (!createIndexResponse.IsValid)
            {
                // Fallback to standard tokenizer if vi_tokenizer is not installed
                if (createIndexResponse.ServerError?.Error?.Reason?.Contains("Unknown tokenizer [vi_tokenizer]") == true)
                {
                    await _elasticClient.Indices.CreateAsync(IndexName, c => c
                        .Settings(s => s
                            .Analysis(a => a
                                .Analyzers(an => an
                                    .Custom("vietnamese_analyzer", ca => ca
                                        .Tokenizer("standard")
                                        .Filters("lowercase")
                                    )
                                )
                            )
                        )
                        .Map<Equipment>(m => m
                            .Properties(p => p
                                .Text(t => t
                                    .Name(n => n.Name)
                                    .Analyzer("vietnamese_analyzer")
                                )
                                .Text(t => t
                                    .Name(n => n.Code)
                                    .Analyzer("vietnamese_analyzer")
                                )
                                .Text(t => t
                                    .Name(n => n.SerialNumber)
                                    .Analyzer("vietnamese_analyzer")
                                )
                            )
                        )
                    );
                }
            }
        }
    }

    public async Task<IEnumerable<Equipment>> SearchEquipmentsAsync(string keyword, IEnumerable<long>? unitIds = null)
    {
        var searchResponse = await _elasticClient.SearchAsync<Equipment>(s => s
            .Index(IndexName)
            .Query(q => q
                .Bool(b => b
                    .Must(m => m
                        .MultiMatch(mm => mm
                            .Fields(f => f
                                .Field(p => p.Name)
                                .Field(p => p.Code)
                                .Field(p => p.SerialNumber)
                            )
                            .Query(keyword)
                        )
                    )
                    .Filter(f => {
                        if (unitIds != null && unitIds.Any())
                        {
                            return f.Terms(t => t
                                .Field(p => p.UnitId)
                                .Terms(unitIds)
                            );
                        }
                        return null;
                    })
                )
            )
        );

        return searchResponse.Documents;
    }
}
