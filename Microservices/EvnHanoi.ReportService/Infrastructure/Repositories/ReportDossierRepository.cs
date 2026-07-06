using System.Data;
using Dapper;
using EvnHanoi.ReportService.Core.Interfaces;
using EvnHanoi.ReportService.Core.Models;

namespace EvnHanoi.ReportService.Infrastructure.Repositories;

public class ReportDossierRepository : IReportDossierRepository
{
    private readonly IDbConnection _connection;

    public ReportDossierRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<IEnumerable<ReportDossierLookupItem>> GetOrganizationUnitsAsync(bool isAdmin, long? userUnitId)
    {
        EnsureOpen();

        if (!isAdmin && userUnitId.HasValue)
        {
            const string sql = @"
                SELECT CAST(Id AS VARCHAR2(50)) AS Id, Name, Code
                FROM ORGANIZATION_UNIT
                START WITH Id = :StartUnitId
                CONNECT BY PRIOR Id = ParentId
                ORDER SIBLINGS BY Name";
            return await _connection.QueryAsync<ReportDossierLookupItem>(sql, new { StartUnitId = userUnitId.Value });
        }

        const string allSql = @"
            SELECT CAST(Id AS VARCHAR2(50)) AS Id, Name, Code
            FROM ORGANIZATION_UNIT
            ORDER BY Name";
        return await _connection.QueryAsync<ReportDossierLookupItem>(allSql);
    }

    public async Task<IEnumerable<ReportDossierLookupItem>> GetGridTypesAsync(long? unitScopeRoot)
    {
        EnsureOpen();
        // Danh mục loại lưới điện (GridTypes) — đồng bộ Equipment GET grid-types/lookup.
        // Filter báo cáo theo gridTypeId; không giới hạn chỉ loại đã có hồ sơ xuất bản.
        const string sql = @"
            SELECT CAST(Id AS VARCHAR2(50)) AS Id, Name, CAST(Id AS VARCHAR2(50)) AS Code
            FROM GridTypes
            ORDER BY Id ASC";
        return await _connection.QueryAsync<ReportDossierLookupItem>(sql);
    }

    public Task<IEnumerable<ReportDossierLookupItem>> GetEquipmentsAsync(long? unitScopeRoot, long? filterUnitId) =>
        QueryPublishedLookupAsync(
            unitScopeRoot,
            filterUnitId,
            """
            SELECT DISTINCT CAST(e.Id AS VARCHAR2(50)) AS Id, e.Name AS Name, e.Code AS Code
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
            INNER JOIN Equipments e ON de.EquipmentId = e.Id AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
            """);

    public Task<IEnumerable<ReportDossierLookupItem>> GetInfrastructuresAsync(
        long? unitScopeRoot,
        long? filterUnitId,
        int infraTypeId) =>
        QueryPublishedLookupAsync(
            unitScopeRoot,
            filterUnitId,
            $"""
            SELECT DISTINCT CAST(i.ID AS VARCHAR2(50)) AS Id, i.NAME AS Name, i.CODE AS Code
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID AND i.INFRA_TYPE_ID = {infraTypeId}
            """);

    public async Task<IEnumerable<ReportDossierBhsColumn>> GetBhsColumnsAsync()
    {
        EnsureOpen();
        const string sql = @"
            SELECT c.Name AS Key, c.Code, c.Name AS Label, c.Priority
            FROM CATALOG c
            INNER JOIN CATALOG_TYPE ct ON c.CatalogTypeId = ct.Id
            WHERE ct.Code = 'BHS'
              AND c.IsDeleted = 0
              AND ct.IsDeleted = 0
            ORDER BY c.Priority ASC, c.Name ASC";
        return await _connection.QueryAsync<ReportDossierBhsColumn>(sql);
    }

    public async Task<Dictionary<long, string>> GetUnitNamesAsync(IEnumerable<long> unitIds)
    {
        var ids = unitIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<long, string>();

        EnsureOpen();
        const string sql = "SELECT Id, Name FROM ORGANIZATION_UNIT WHERE Id IN :UnitIds";
        var rows = await _connection.QueryAsync<(long Id, string Name)>(sql, new { UnitIds = ids });
        return rows.ToDictionary(r => r.Id, r => r.Name);
    }

    public async Task<IReadOnlyList<long>> ResolveUnitScopeIdsAsync(long unitId)
    {
        EnsureOpen();
        const string sql = @"
            SELECT Id
            FROM ORGANIZATION_UNIT
            START WITH Id = :StartUnitId
            CONNECT BY PRIOR Id = ParentId";
        var unitIds = await _connection.QueryAsync<long>(sql, new { StartUnitId = unitId });
        return unitIds.Distinct().ToList();
    }

    public async Task<IReadOnlyList<string>> ResolveInfrastructureScopeIdsAsync(int infraTypeId, IReadOnlyList<long>? unitScopeIds)
    {
        EnsureOpen();
        var sql = @"
            SELECT DISTINCT i.ID
            FROM INFRASTRUCTURE i
            INNER JOIN DOSSIERS d ON d.InfrastructureId = i.ID
            WHERE i.INFRA_TYPE_ID = :InfraTypeId
              AND (d.IsDeleted = 0 OR d.IsDeleted IS NULL)
              AND d.STATUS_ID = 6
              AND d.PUBLISHSTATUSID = 2";

        var parameters = new DynamicParameters();
        parameters.Add("InfraTypeId", infraTypeId);

        if (unitScopeIds is { Count: > 0 })
        {
            sql += " AND i.UNIT_ID IN :UnitScopeIds";
            parameters.Add("UnitScopeIds", unitScopeIds.ToArray());
        }

        var infraIds = (await _connection.QueryAsync<string>(sql, parameters))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return infraIds.Count > 0 ? infraIds : new List<string> { "__none__" };
    }

    private async Task<IEnumerable<ReportDossierLookupItem>> QueryPublishedLookupAsync(
        long? unitScopeRoot,
        long? filterUnitId,
        string selectFromSql)
    {
        EnsureOpen();
        var parameters = new DynamicParameters();
        var sql = selectFromSql + @"
            WHERE d.IsDeleted = 0
              AND d.STATUS_ID = 6
              AND d.PUBLISHSTATUSID = 2";

        AppendUnitFilter(ref sql, parameters, unitScopeRoot, filterUnitId);
        sql += " ORDER BY Name";
        return await _connection.QueryAsync<ReportDossierLookupItem>(sql, parameters);
    }

    private static void AppendUnitFilter(
        ref string sql,
        DynamicParameters parameters,
        long? unitScopeRoot,
        long? filterUnitId)
    {
        var effectiveUnitId = filterUnitId ?? unitScopeRoot;
        if (!effectiveUnitId.HasValue)
            return;

        parameters.Add("FilterUnitId", effectiveUnitId.Value);
        sql += @"
              AND i.UNIT_ID IN (
                    SELECT Id
                    FROM ORGANIZATION_UNIT
                    START WITH Id = :FilterUnitId
                    CONNECT BY PRIOR Id = ParentId
              )";
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
    }
}
