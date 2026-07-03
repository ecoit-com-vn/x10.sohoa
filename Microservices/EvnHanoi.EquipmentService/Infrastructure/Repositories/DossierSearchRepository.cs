using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Infrastructure.Models;
using EvnHanoi.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Nest;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class DossierSearchRepository : IDossierSearchRepository
{
    private readonly IElasticClient _elasticClient;
    private readonly ILogger<DossierSearchRepository> _logger;

    public DossierSearchRepository(IElasticClient elasticClient, ILogger<DossierSearchRepository> logger)
    {
        _elasticClient = elasticClient;
        _logger = logger;
    }

    public async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(DossierFilterDto filter)
    {
        var from = (filter.Page - 1) * filter.PageSize;
        var keyword = filter.Keyword?.Trim();

        var response = await _elasticClient.SearchAsync<DossierEsDocument>(s => s
            .Index(DossierMessaging.IndexName)
            .From(from)
            .Size(filter.PageSize)
            .TrackTotalHits(true)
            .Sort(sort => sort.Descending(f => f.CreatedDate))
            .Query(q => BuildQuery(q, filter, keyword))
        );

        if (!response.IsValid)
        {
            _logger.LogError(
                "Elasticsearch dossier search failed: {Error}",
                response.ServerError?.Error?.Reason ?? response.OriginalException?.Message);
            throw new InvalidOperationException("Không thể truy vấn danh sách hồ sơ từ Elasticsearch.");
        }

        var items = response.Documents.Select(MapToListItem).ToList();
        var totalCount = (int)response.Total;
        return (items, totalCount);
    }

    private static QueryContainer BuildQuery(QueryContainerDescriptor<DossierEsDocument> q, DossierFilterDto filter, string? keyword)
    {
        return q.Bool(b =>
        {
            b.MustNot(mn => mn.Term(t => t.Field(f => f.IsDeleted).Value(true)));

            var filters = new List<QueryContainer>();

            if (filter.GridTypeId.HasValue)
                filters.Add(q.Term(t => t.Field(doc => doc.GridTypeId).Value(filter.GridTypeId.Value)));

            if (filter.InfrastructureId.HasValue)
            {
                var infraId = filter.InfrastructureId.Value.ToString();
                filters.Add(q.Term(t => t.Field(doc => doc.InfrastructureId).Value(infraId)));
            }

            if (filter.DossierTypeId.HasValue)
            {
                var dossierTypeId = filter.DossierTypeId.Value.ToString();
                filters.Add(q.Term(t => t.Field(doc => doc.DossierTypeId).Value(dossierTypeId)));
            }

            if (filter.UnitScopeIds is { Count: > 0 })
                filters.Add(q.Terms(t => t.Field(doc => doc.UnitId).Terms(filter.UnitScopeIds)));

            if (filter.StatusId.HasValue && filter.StatusId.Value > 0)
                filters.Add(q.Term(t => t.Field(doc => doc.StatusId).Value(filter.StatusId.Value)));

            if (filters.Count > 0)
                b.Filter(filters.ToArray());

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                b.Should(
                    sh => sh.Nested(n => n
                        .Path(p => p.FormFields)
                        .Query(nq => nq.Match(m => m
                            .Field("formFields.textValue")
                            .Query(keyword)
                        ))
                    ),
                    sh => sh.Nested(n => n
                        .Path(p => p.CatalogFields)
                        .Query(nq => nq.Match(m => m
                            .Field("catalogFields.value")
                            .Query(keyword)
                        ))
                    ),
                    sh => sh.Match(m => m
                        .Field(f => f.InfrastructureName)
                        .Query(keyword)
                    ),
                    sh => sh.Match(m => m
                        .Field(f => f.InfrastructureCode)
                        .Query(keyword)
                    ),
                    sh => sh.Match(m => m
                        .Field(f => f.CreatorName)
                        .Query(keyword)
                    ),
                    sh => sh.Match(m => m
                        .Field(f => f.DossierSetName)
                        .Query(keyword)
                    ),
                    sh => sh.Match(m => m
                        .Field(f => f.DossierTypeName)
                        .Query(keyword)
                    )
                );
                b.MinimumShouldMatch(1);
            }

            return b;
        });
    }

    private static DossierListItemDto MapToListItem(DossierEsDocument doc)
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
            StatusId = doc.StatusId,
            StatusCode = doc.StatusCode,
            StatusName = doc.StatusName,
            WorkflowStatusName = doc.WorkflowStatusName,
            DocumentCount = doc.DocumentCount,
            Creator = new CreatorInfoDto
            {
                Id = doc.CreatorId ?? string.Empty,
                Username = doc.CreatorUsername ?? string.Empty,
                Name = doc.CreatorName ?? string.Empty
            },
            CreatedDate = doc.CreatedDate,
            CatalogData = doc.CatalogFields
                .OrderBy(c => c.SortOrder)
                .GroupBy(c => c.CatalogName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase)
        };
    }
}
