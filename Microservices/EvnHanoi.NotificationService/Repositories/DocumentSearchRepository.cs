using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Services;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.NotificationService.Repositories;

public class DocumentSearchRepository : IDocumentSearchRepository
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<DocumentSearchRepository> _logger;

    public DocumentSearchRepository(ElasticsearchClient client, ILogger<DocumentSearchRepository> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<DocumentSearchItemDto> Items, int TotalCount)> SearchAsync(
        DocumentSearchFilterDto filter)
    {
        var keyword = filter.Keyword?.Trim();
        var from = (filter.Page - 1) * filter.PageSize;
        var sortMode = filter.Sort?.Trim().ToLowerInvariant() ?? "newest";

        var response = await _client.SearchAsync<DocumentEsDocument>(s => s
            .Indices(DocumentTextMessaging.IndexName)
            .From(from)
            .Size(filter.PageSize)
            .TrackTotalHits(true)
            .Sort(sort => ConfigureSort(sort, sortMode, keyword))
            .Query(q => ConfigureQuery(q, filter, keyword))
            .Highlight(new Highlight
            {
                Fields = new Dictionary<Field, HighlightField>
                {
                    { DocumentEsFieldNames.FullText, new HighlightField { FragmentSize = 200, NumberOfFragments = 1 } },
                    { DocumentEsFieldNames.DocumentName, new HighlightField { FragmentSize = 120, NumberOfFragments = 1 } }
                },
                PreTags = new[] { "<em>" },
                PostTags = new[] { "</em>" }
            })
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError(
                "Elasticsearch document search failed: {Error}",
                response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation);
            throw new InvalidOperationException("Không thể truy vấn tài liệu từ Elasticsearch.");
        }

        var items = response.Hits
            .Select(MapHit)
            .ToList();

        return (items, (int)response.Total);
    }

    public async Task<DocumentEsDocument?> GetByVersionIdAsync(string documentVersionId)
    {
        var normalizedId = DossierIndexIdNormalizer.Normalize(documentVersionId);
        if (string.IsNullOrEmpty(normalizedId))
            return null;

        var response = await _client.GetAsync<DocumentEsDocument>(
            DocumentTextMessaging.IndexName,
            normalizedId);

        if (!response.Found || response.Source is null)
            return null;

        return response.Source;
    }

    private static void ConfigureSort(
        SortOptionsDescriptor<DocumentEsDocument> sort,
        string sortMode,
        string? keyword)
    {
        if (sortMode == "relevance" && !string.IsNullOrWhiteSpace(keyword))
        {
            sort.Score(sc => sc.Order(SortOrder.Desc));
            return;
        }

        if (sortMode == "oldest")
        {
            sort.Field(DocumentEsFieldNames.IndexedAt, fs => fs.Order(SortOrder.Asc));
            return;
        }

        sort.Field(DocumentEsFieldNames.IndexedAt, fs => fs.Order(SortOrder.Desc));
    }

    private static void ConfigureQuery(
        QueryDescriptor<DocumentEsDocument> q,
        DocumentSearchFilterDto filter,
        string? keyword)
    {
        q.Bool(b =>
        {
            var mustQueries = new List<Query>();
            var shouldQueries = new List<Query>();
            var filterQueries = new List<Query>
            {
                new QueryDescriptor<DocumentEsDocument>().Term(t => t
                    .Field(DocumentEsFieldNames.IsDeleted)
                    .Value(false)),
                new QueryDescriptor<DocumentEsDocument>().Term(t => t
                    .Field(DocumentEsFieldNames.StatusId)
                    .Value(DocumentSearchConstants.ApprovedStatusId)),
                new QueryDescriptor<DocumentEsDocument>().Term(t => t
                    .Field(DocumentEsFieldNames.PublishStatusId)
                    .Value(DocumentSearchConstants.PublishedStatusId))
            };

            if (filter.UnitScopeIds is { Count: > 0 })
            {
                filterQueries.Add(new QueryDescriptor<DocumentEsDocument>().Terms(t => t
                    .Field(DocumentEsFieldNames.UnitId)
                    .Terms(new TermsQueryField(filter.UnitScopeIds.Select(FieldValue.Long).ToArray()))));
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                mustQueries.Add(new QueryDescriptor<DocumentEsDocument>().MultiMatch(mm => mm
                    .Query(keyword)
                    .Fields(new[]
                    {
                        $"{DocumentEsFieldNames.DocumentName}^2",
                        DocumentEsFieldNames.FullText,
                        "extractionSummary",
                        DocumentEsFieldNames.InfrastructureName,
                        "equipmentNames",
                        "dossierTypeName",
                        "documentTypeName"
                    })));

                shouldQueries.Add(new QueryDescriptor<DocumentEsDocument>().MatchPhrase(mp => mp
                    .Field(DocumentEsFieldNames.FullText)
                    .Query(keyword)
                    .Slop(2)
                    .Boost(8)));

                shouldQueries.Add(new QueryDescriptor<DocumentEsDocument>().MatchPhrase(mp => mp
                    .Field(DocumentEsFieldNames.DocumentName)
                    .Query(keyword)
                    .Slop(2)
                    .Boost(4)));
            }

            if (mustQueries.Count > 0)
                b.Must(mustQueries);
            if (shouldQueries.Count > 0)
                b.Should(shouldQueries);
            if (filterQueries.Count > 0)
                b.Filter(filterQueries);
        });
    }

    private static DocumentSearchItemDto MapHit(Hit<DocumentEsDocument> hit)
    {
        var doc = hit.Source ?? new DocumentEsDocument();
        return new DocumentSearchItemDto
        {
            DocumentVersionId = doc.DocumentVersionId,
            DocumentId = doc.DocumentId,
            DocumentName = doc.DocumentName,
            Highlight = ResolveHighlight(hit.Highlight),
            MimeType = doc.MimeType,
            DossierId = doc.DossierId,
            DossierTitle = doc.DossierTitle,
            InfrastructureName = doc.InfrastructureName,
            DossierTypeName = doc.DossierTypeName,
            DocumentTypeName = doc.DocumentTypeName,
            EquipmentNames = doc.EquipmentNames,
            IndexedAt = doc.IndexedAt
        };
    }

    private static string? ResolveHighlight(IReadOnlyDictionary<string, IReadOnlyCollection<string>>? highlight)
    {
        if (highlight is null || highlight.Count == 0)
            return null;

        if (highlight.TryGetValue(DocumentEsFieldNames.FullText, out var fullTextFragments) &&
            fullTextFragments.Count > 0)
            return fullTextFragments.First();

        if (highlight.TryGetValue(DocumentEsFieldNames.DocumentName, out var nameFragments) &&
            nameFragments.Count > 0)
            return nameFragments.First();

        return highlight.Values.FirstOrDefault()?.FirstOrDefault();
    }
}
