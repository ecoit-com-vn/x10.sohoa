using System.Data;
using Dapper;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Repositories;

namespace EvnHanoi.NotificationService.Services;

public class DossierSearchService : IDossierSearchService
{
    private readonly IDossierSearchRepository _searchRepository;
    private readonly IDossierEnrichmentRepository _enrichmentRepository;
    private readonly IDbConnection _dbConnection;

    public DossierSearchService(
        IDossierSearchRepository searchRepository,
        IDossierEnrichmentRepository enrichmentRepository,
        IDbConnection dbConnection)
    {
        _searchRepository = searchRepository;
        _enrichmentRepository = enrichmentRepository;
        _dbConnection = dbConnection;
    }

    public Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(DossierFilterDto filter) =>
        SearchAsync(filter);

    public Task<DossierTabCountsDto> GetTabCountsAsync(DossierFilterDto filter) =>
        ResolveUnitScopeAndSearchCountsAsync(filter);

    private async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> SearchAsync(DossierFilterDto filter)
    {
        await ApplyUnitScopeAsync(filter);
        var bhsCatalogs = (await _enrichmentRepository.GetBhsCatalogDefinitionsAsync()).ToList();
        return await _searchRepository.GetPagedAsync(filter, bhsCatalogs);
    }

    private async Task<DossierTabCountsDto> ResolveUnitScopeAndSearchCountsAsync(DossierFilterDto filter)
    {
        await ApplyUnitScopeAsync(filter);
        return await _searchRepository.GetTabCountsAsync(filter);
    }

    private async Task ApplyUnitScopeAsync(DossierFilterDto filter)
    {
        if (!filter.UnitId.HasValue)
            return;

        if (_dbConnection.State != ConnectionState.Open)
            _dbConnection.Open();

        const string sql = @"
            SELECT Id
            FROM ORGANIZATION_UNIT
            START WITH Id = :StartUnitId
            CONNECT BY PRIOR Id = ParentId";

        var unitIds = await _dbConnection.QueryAsync<long>(sql, new { StartUnitId = filter.UnitId.Value });
        filter.UnitScopeIds = unitIds.Distinct().ToList();
    }
}
