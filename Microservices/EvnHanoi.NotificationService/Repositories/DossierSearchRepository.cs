using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Services;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.NotificationService.Repositories;

public class DossierSearchRepository : IDossierSearchRepository
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<DossierSearchRepository> _logger;

    public DossierSearchRepository(ElasticsearchClient client, ILogger<DossierSearchRepository> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(
        DossierFilterDto filter,
        IReadOnlyList<BhsCatalogDefinition> bhsCatalogs)
    {
        var from = (filter.Page - 1) * filter.PageSize;
        var keyword = filter.Keyword?.Trim();

        var response = await _client.SearchAsync<DossierEsDocument>(s => s
            .Indices(DossierMessaging.IndexName)
            .From(from)
            .Size(filter.PageSize)
            .TrackTotalHits(true)
            .Sort(sort => sort.Field(f => f.CreatedDate, fs => fs.Order(SortOrder.Desc)))
            .Query(q => { ConfigureQuery(q, filter, keyword); })
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError(
                "Elasticsearch dossier search failed: {Error}",
                response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation);
            throw new InvalidOperationException("Không thể truy vấn danh sách hồ sơ từ Elasticsearch.");
        }

        var items = response.Documents.Select(doc => MapToListItem(doc, bhsCatalogs)).ToList();
        var totalCount = (int)response.Total;
        return (items, totalCount);
    }

    internal static void ConfigureQuery(
        QueryDescriptor<DossierEsDocument> q,
        DossierFilterDto filter,
        string? keyword)
    {
        q.Bool(b =>
        {
            b.MustNot(mn => mn.Term(t => t.Field(f => f.IsDeleted).Value(true)));

            var filters = new List<Action<QueryDescriptor<DossierEsDocument>>>();

            if (filter.GridTypeId.HasValue)
                filters.Add(f => f.Term(t => t.Field(doc => doc.GridTypeId).Value(filter.GridTypeId.Value)));

            if (filter.InfrastructureId.HasValue)
            {
                var infraId = filter.InfrastructureId.Value.ToString();
                filters.Add(f => f.Term(t => t.Field(doc => doc.InfrastructureId).Value(infraId)));
            }

            if (filter.DossierTypeId.HasValue)
            {
                var dossierTypeId = filter.DossierTypeId.Value.ToString();
                filters.Add(f => f.Term(t => t.Field(doc => doc.DossierTypeId).Value(dossierTypeId)));
            }

            if (filter.UnitScopeIds is { Count: > 0 })
            {
                filters.Add(f => f.Terms(t => t
                    .Field(doc => doc.UnitId)
                    .Terms(new TermsQueryField(filter.UnitScopeIds.Select(FieldValue.Long).ToArray()))));
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
                filters.Add(f => f.Term(t => t.Field(doc => doc.Status).Value(filter.Status)));

            if (filters.Count > 0)
                b.Filter(filters.ToArray());

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                b.Must(m => m.Bool(bb => bb
                    .Should(
                        sh => sh.Nested(n => n
                            .Path(p => p.FormFields)
                            .Query(nq => nq.Match(mq => mq
                                .Field("formFields.textValue")
                                .Query(keyword)
                            ))
                        ),
                        sh => sh.Nested(n => n
                            .Path(p => p.CatalogFields)
                            .Query(nq => nq.Match(mq => mq
                                .Field("catalogFields.value")
                                .Query(keyword)
                            ))
                        ),
                        sh => sh.Match(mq => mq.Field(f => f.InfrastructureName).Query(keyword)),
                        sh => sh.Match(mq => mq.Field(f => f.InfrastructureCode).Query(keyword)),
                        sh => sh.Match(mq => mq.Field(f => f.CreatorName).Query(keyword)),
                        sh => sh.Match(mq => mq.Field(f => f.DossierSetName).Query(keyword)),
                        sh => sh.Match(mq => mq.Field(f => f.DossierTypeName).Query(keyword))
                    )
                    .MinimumShouldMatch(1)
                ));
            }
        });
    }

    private static DossierListItemDto MapToListItem(
        DossierEsDocument doc,
        IReadOnlyList<BhsCatalogDefinition> bhsCatalogs)
    {
        return new DossierListItemDto
        {
            Id = Guid.TryParse(doc.Id, out var id) ? id : Guid.Empty,
            GridTypeId = doc.GridTypeId,
            GridTypeName = doc.GridTypeName,
            InfrastructureId = Guid.TryParse(doc.InfrastructureId, out var infraId) ? infraId : null,
            InfrastructureName = doc.InfrastructureName,
            InfrastructureCode = doc.InfrastructureCode,
            DossierSetId = Guid.TryParse(doc.DossierSetId, out var setId) ? setId : null,
            DossierSetName = doc.DossierSetName,
            DossierTypeId = Guid.TryParse(doc.DossierTypeId, out var typeId) ? typeId : Guid.Empty,
            DossierTypeName = doc.DossierTypeName,
            Status = doc.Status,
            WorkflowStatusName = doc.WorkflowStatusName,
            DocumentCount = doc.DocumentCount,
            CreatorName = doc.CreatorName,
            CreatedDate = doc.CreatedDate,
            CatalogData = DossierCatalogDataMapper.ToCatalogData(doc.CatalogFields, doc.FormFields, bhsCatalogs)
        };
    }
}
