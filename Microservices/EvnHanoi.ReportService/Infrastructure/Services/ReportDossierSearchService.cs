using EvnHanoi.ReportService.Core.Interfaces;
using EvnHanoi.ReportService.Core.Models;
using EvnHanoi.ReportService.Infrastructure.Elasticsearch;

namespace EvnHanoi.ReportService.Infrastructure.Services;

public class ReportDossierSearchService : IReportDossierSearchService
{
    private readonly ReportDossierEsSearchRepository _esRepository;
    private readonly IReportDossierRepository _repository;

    public ReportDossierSearchService(
        ReportDossierEsSearchRepository esRepository,
        IReportDossierRepository repository)
    {
        _esRepository = esRepository;
        _repository = repository;
    }

    public async Task<ReportDossierSearchResponse> SearchAsync(
        ReportDossierSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<long>? unitScopeIds = null;
        if (request.UnitId.HasValue)
            unitScopeIds = await _repository.ResolveUnitScopeIdsAsync(request.UnitId.Value);

        IReadOnlyList<string>? infrastructureScopeIds = null;
        if (request.InfrastructureTypeId.HasValue && !request.InfrastructureId.HasValue)
        {
            infrastructureScopeIds = await _repository.ResolveInfrastructureScopeIdsAsync(
                request.InfrastructureTypeId.Value,
                unitScopeIds);
        }

        var bhsCatalogs = (await _repository.GetBhsColumnsAsync())
            .Select(c => new BhsCatalogDefinition
            {
                Code = c.Code,
                Name = c.Label,
                Priority = c.Priority
            })
            .ToList();

        var (items, totalCount) = await _esRepository.SearchPublishedAsync(
            request,
            bhsCatalogs,
            unitScopeIds,
            infrastructureScopeIds,
            cancellationToken);

        var enriched = await EnrichUnitNamesAsync(items.ToList());

        return new ReportDossierSearchResponse
        {
            Items = enriched,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private async Task<List<ReportDossierListItem>> EnrichUnitNamesAsync(List<ReportDossierListItem> items)
    {
        var missingUnitIds = items
            .Where(i => i.UnitId.HasValue && string.IsNullOrWhiteSpace(i.UnitName))
            .Select(i => i.UnitId!.Value)
            .Distinct()
            .ToList();

        if (missingUnitIds.Count == 0)
            return items;

        var names = await _repository.GetUnitNamesAsync(missingUnitIds);
        foreach (var item in items)
        {
            if (item.UnitId.HasValue && string.IsNullOrWhiteSpace(item.UnitName) &&
                names.TryGetValue(item.UnitId.Value, out var name))
            {
                item.UnitName = name;
            }
        }

        return items;
    }
}
