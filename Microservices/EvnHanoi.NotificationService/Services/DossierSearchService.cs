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
        await ApplyEquipmentTypeScopeAsync(filter);
        await ApplyInfrastructureTypeScopeAsync(filter);
        var bhsCatalogs = (await _enrichmentRepository.GetBhsCatalogDefinitionsAsync()).ToList();
        var (items, totalCount) = await _searchRepository.GetPagedAsync(filter, bhsCatalogs);
        var enriched = await EnrichUnitNamesAsync(items.ToList());
        return (await EnrichInfrastructuresAsync(enriched), totalCount);
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

    private async Task ApplyEquipmentTypeScopeAsync(DossierFilterDto filter)
    {
        if (filter.EquipmentId.HasValue || !filter.EquipmentTypeId.HasValue)
            return;

        if (_dbConnection.State != ConnectionState.Open)
            _dbConnection.Open();

        var sql = @"
            SELECT DISTINCT e.Id
            FROM Equipments e
            WHERE (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
              AND e.EquipmentTypeId = :EquipmentTypeId";

        var parameters = new DynamicParameters();
        parameters.Add("EquipmentTypeId", filter.EquipmentTypeId.Value.ToString());

        if (filter.InfrastructureId.HasValue)
        {
            sql += " AND e.INFRASTRUCTURE_ID = :InfrastructureId";
            parameters.Add("InfrastructureId", filter.InfrastructureId.Value.ToString());
        }

        var equipmentIds = (await _dbConnection.QueryAsync<string>(sql, parameters))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        filter.EquipmentScopeIds = equipmentIds;
    }

    private async Task ApplyInfrastructureTypeScopeAsync(DossierFilterDto filter)
    {
        if (!filter.InfrastructureTypeId.HasValue || filter.InfrastructureId.HasValue)
            return;

        if (_dbConnection.State != ConnectionState.Open)
            _dbConnection.Open();

        var sql = @"
            SELECT DISTINCT i.ID
            FROM INFRASTRUCTURE i
            INNER JOIN DOSSIERS d ON d.InfrastructureId = i.ID
            WHERE i.INFRA_TYPE_ID = :InfraTypeId
              AND (d.IsDeleted = 0 OR d.IsDeleted IS NULL)
              AND d.STATUS_ID = 6
              AND d.PUBLISHSTATUSID = 2";

        var parameters = new DynamicParameters();
        parameters.Add("InfraTypeId", filter.InfrastructureTypeId.Value);

        if (filter.UnitScopeIds is { Count: > 0 })
        {
            sql += " AND i.UNIT_ID IN :UnitScopeIds";
            parameters.Add("UnitScopeIds", filter.UnitScopeIds.ToArray());
        }

        var infraIds = (await _dbConnection.QueryAsync<string>(sql, parameters))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        filter.InfrastructureScopeIds = infraIds.Count > 0
            ? infraIds
            : new List<string> { "__none__" };
    }

    private async Task<List<DossierListItemDto>> EnrichUnitNamesAsync(List<DossierListItemDto> items)
    {
        var missingUnitIds = items
            .Where(i => i.UnitId.HasValue && string.IsNullOrWhiteSpace(i.UnitName))
            .Select(i => i.UnitId!.Value)
            .Distinct()
            .ToList();

        if (missingUnitIds.Count == 0)
            return items;

        if (_dbConnection.State != ConnectionState.Open)
            _dbConnection.Open();

        const string sql = "SELECT Id, Name FROM ORGANIZATION_UNIT WHERE Id IN :UnitIds";
        var rows = (await _dbConnection.QueryAsync<(long Id, string Name)>(sql, new { UnitIds = missingUnitIds.ToArray() }))
            .ToDictionary(r => r.Id, r => r.Name);

        foreach (var item in items)
        {
            if (item.UnitId.HasValue && string.IsNullOrWhiteSpace(item.UnitName) &&
                rows.TryGetValue(item.UnitId.Value, out var name))
            {
                item.UnitName = name;
            }
        }

        return items;
    }

    private async Task<List<DossierListItemDto>> EnrichInfrastructuresAsync(List<DossierListItemDto> items)
    {
        var dossierIds = items
            .Select(item => item.Id)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Select(id => id.ToString())
            .ToArray();
        if (dossierIds.Length == 0) return items;

        if (_dbConnection.State != ConnectionState.Open)
            _dbConnection.Open();

        const string sql = @"
            SELECT DISTINCT
                source.DossierId,
                source.InfrastructureId,
                i.CODE AS InfrastructureCode,
                i.NAME AS InfrastructureName
            FROM (
                SELECT di.DossierId, di.InfrastructureId
                FROM DOSSIER_INFRASTRUCTURE di
                WHERE di.DossierId IN :DossierIds
                UNION
                SELECT d.Id AS DossierId, d.InfrastructureId
                FROM DOSSIERS d
                WHERE d.Id IN :DossierIds
                  AND d.InfrastructureId IS NOT NULL
            ) source
            INNER JOIN INFRASTRUCTURE i ON i.ID = source.InfrastructureId
            WHERE i.IsDeleted = 0 OR i.IsDeleted IS NULL
            ORDER BY source.DossierId, i.NAME";

        var assignments = await _dbConnection.QueryAsync<DossierInfrastructureAssignmentDto>(
            sql,
            new { DossierIds = dossierIds });
        var byDossier = assignments
            .GroupBy(assignment => assignment.DossierId)
            .ToDictionary(group => group.Key, group => group.Cast<DossierInfrastructureDto>().ToList());

        foreach (var item in items)
        {
            if (!byDossier.TryGetValue(item.Id, out var infrastructures)) continue;

            item.Infrastructures = infrastructures;
            item.InfrastructureName = string.Join(", ", infrastructures
                .Select(infrastructure => infrastructure.InfrastructureName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct());
            item.InfrastructureCode = string.Join(", ", infrastructures
                .Select(infrastructure => infrastructure.InfrastructureCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct());
        }

        return items;
    }
}
