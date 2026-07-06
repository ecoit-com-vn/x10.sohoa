using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.ReportService.Core.Interfaces;
using EvnHanoi.ReportService.Core.Models;
using EvnHanoi.ReportService.Infrastructure.Services;

namespace EvnHanoi.ReportService.Infrastructure.Elasticsearch;

public class ReportDossierEsSearchRepository
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ReportDossierEsSearchRepository> _logger;

    public ReportDossierEsSearchRepository(ElasticsearchClient client, ILogger<ReportDossierEsSearchRepository> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<ReportDossierListItem> Items, int TotalCount)> SearchPublishedAsync(
        ReportDossierSearchRequest request,
        IReadOnlyList<BhsCatalogDefinition> bhsCatalogs,
        IReadOnlyList<long>? unitScopeIds,
        IReadOnlyList<string>? infrastructureScopeIds,
        CancellationToken cancellationToken = default)
    {
        var from = (request.Page - 1) * request.PageSize;

        var response = await _client.SearchAsync<ReportDossierEsDocument>(s => s
            .Indices(DossierMessaging.IndexName)
            .From(from)
            .Size(request.PageSize)
            .TrackTotalHits(true)
            .Sort(sort => sort.Field(f => f.CreatedDate, fs => fs.Order(SortOrder.Desc)))
            .Query(q => ConfigureQuery(q, request, unitScopeIds, infrastructureScopeIds)),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            var errorDetail = response.ElasticsearchServerError?.Error?.Reason
                ?? response.ElasticsearchServerError?.Error?.RootCause?.FirstOrDefault()?.Reason
                ?? response.DebugInformation;
            _logger.LogError("Report dossier ES search failed: {Error}", errorDetail);
            throw new InvalidOperationException($"Không thể truy vấn dữ liệu báo cáo từ Elasticsearch: {errorDetail}");
        }

        var items = response.Documents
            .Select(doc => MapToListItem(doc, bhsCatalogs))
            .ToList();

        return (items, (int)response.Total);
    }

    private static void ConfigureQuery(
        QueryDescriptor<ReportDossierEsDocument> q,
        ReportDossierSearchRequest request,
        IReadOnlyList<long>? unitScopeIds,
        IReadOnlyList<string>? infrastructureScopeIds)
    {
        q.Bool(b =>
        {
            var mustQueries = new List<Query>
            {
                new QueryDescriptor<ReportDossierEsDocument>().Term(t => t
                    .Field(ReportDossierEsFieldNames.PublishStatusId)
                    .Value(2))
            };
            var mustNotQueries = new List<Query>
            {
                new QueryDescriptor<ReportDossierEsDocument>().Term(t => t
                    .Field(ReportDossierEsFieldNames.IsDeleted)
                    .Value(true))
            };
            var filterQueries = new List<Query>();

            if (request.GridTypeId.HasValue)
            {
                filterQueries.Add(new QueryDescriptor<ReportDossierEsDocument>().Term(t => t
                    .Field(ReportDossierEsFieldNames.GridTypeId)
                    .Value(request.GridTypeId.Value)));
            }

            if (request.InfrastructureId.HasValue)
            {
                var infraVariants = DossierIndexIdNormalizer
                    .GetGuidTermVariants(request.InfrastructureId.Value.ToString())
                    .Select(FieldValue.String)
                    .ToArray();
                filterQueries.Add(new QueryDescriptor<ReportDossierEsDocument>().Terms(t => t
                    .Field(ReportDossierEsFieldNames.InfrastructureId)
                    .Terms(new TermsQueryField(infraVariants))));
            }
            else if (infrastructureScopeIds is { Count: > 0 })
            {
                var infraVariants = infrastructureScopeIds
                    .SelectMany(id => DossierIndexIdNormalizer.GetGuidTermVariants(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(FieldValue.String)
                    .ToArray();
                filterQueries.Add(new QueryDescriptor<ReportDossierEsDocument>().Terms(t => t
                    .Field(ReportDossierEsFieldNames.InfrastructureId)
                    .Terms(new TermsQueryField(infraVariants))));
            }

            if (request.EquipmentId.HasValue)
            {
                var equipmentVariants = DossierIndexIdNormalizer
                    .GetGuidTermVariants(request.EquipmentId.Value.ToString())
                    .Select(FieldValue.String)
                    .ToArray();
                filterQueries.Add(new QueryDescriptor<ReportDossierEsDocument>().Nested(n => n
                    .Path(p => p.Equipments)
                    .Query(nq => nq.Terms(t => t
                        .Field(ReportDossierEsFieldNames.EquipmentId)
                        .Terms(new TermsQueryField(equipmentVariants))))));
            }

            if (unitScopeIds is { Count: > 0 })
            {
                filterQueries.Add(new QueryDescriptor<ReportDossierEsDocument>().Terms(t => t
                    .Field(ReportDossierEsFieldNames.UnitId)
                    .Terms(new TermsQueryField(unitScopeIds.Select(FieldValue.Long).ToArray()))));
            }

            if (mustQueries.Count > 0)
                b.Must(mustQueries);
            if (mustNotQueries.Count > 0)
                b.MustNot(mustNotQueries);
            if (filterQueries.Count > 0)
                b.Filter(filterQueries);
        });
    }

    private static ReportDossierListItem MapToListItem(
        ReportDossierEsDocument doc,
        IReadOnlyList<BhsCatalogDefinition> bhsCatalogs) =>
        new()
        {
            Id = Guid.TryParse(doc.Id, out var id) ? id : Guid.Empty,
            GridTypeId = doc.GridTypeId,
            GridTypeName = doc.GridTypeName,
            InfrastructureId = Guid.TryParse(doc.InfrastructureId, out var infraId) ? infraId : null,
            InfrastructureName = doc.InfrastructureName,
            InfrastructureCode = doc.InfrastructureCode,
            UnitId = doc.UnitId,
            UnitName = doc.UnitName,
            EquipmentName = doc.Equipments?.FirstOrDefault()?.EquipmentName,
            DossierSetId = Guid.TryParse(doc.DossierSetId, out var setId) ? setId : null,
            DossierSetName = doc.DossierSetName,
            DossierTypeId = Guid.TryParse(doc.DossierTypeId, out var typeId) ? typeId : Guid.Empty,
            DossierTypeName = doc.DossierTypeName,
            StatusId = doc.StatusId,
            StatusCode = doc.StatusCode,
            StatusName = doc.StatusName,
            DocumentCount = doc.DocumentCount,
            Creator = new ReportCreatorInfo
            {
                Id = doc.CreatorId ?? string.Empty,
                Username = doc.CreatorUsername ?? string.Empty,
                Name = doc.CreatorName ?? string.Empty
            },
            CreatedDate = doc.CreatedDate,
            CatalogData = ReportDossierCatalogMapper.ToCatalogData(doc.CatalogFields, doc.FormFields, bhsCatalogs)
        };
}
