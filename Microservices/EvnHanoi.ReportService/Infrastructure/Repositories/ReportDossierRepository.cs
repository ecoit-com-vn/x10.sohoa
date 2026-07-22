using System.Data;
using System.Text.Json;
using Dapper;
using EvnHanoi.ReportService.Core.DTOs;
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
                SELECT CAST(Id AS VARCHAR2(50)) AS Id, Name, Code, ParentId
                FROM ORGANIZATION_UNIT
                START WITH Id = :StartUnitId
                CONNECT BY PRIOR Id = ParentId
                ORDER SIBLINGS BY Name";
            return await _connection.QueryAsync<ReportDossierLookupItem>(sql, new { StartUnitId = userUnitId.Value });
        }

        const string allSql = @"
            SELECT CAST(Id AS VARCHAR2(50)) AS Id, Name, Code, ParentId
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

    public async Task<IEnumerable<ReportDossierLookupItem>> GetEquipmentTypesAsync(long? unitScopeRoot)
    {
        EnsureOpen();
        const string sql = @"
            SELECT CAST(Id AS VARCHAR2(50)) AS Id, Name, Code
            FROM EquipmentTypes
            WHERE NVL(IsDeleted, 0) = 0
            ORDER BY Name ASC";
        return await _connection.QueryAsync<ReportDossierLookupItem>(sql);
    }

    public async Task<IEnumerable<ReportDossierLookupItem>> GetDossierTypesAsync(long? unitScopeRoot)
    {
        EnsureOpen();
        const string sql = @"
            SELECT CAST(ID AS VARCHAR2(50)) AS Id, Name, Code
            FROM DOSSIER_TYPES
            WHERE NVL(IsDeleted, 0) = 0
              AND NVL(IS_ACTIVE, 1) = 1
            ORDER BY Name ASC";
        return await _connection.QueryAsync<ReportDossierLookupItem>(sql);
    }

    public async Task<IEnumerable<ReportDossierLookupItem>> GetDocumentTypesAsync()
    {
        EnsureOpen();
        const string sql = @"
            SELECT CAST(ID AS VARCHAR2(50)) AS Id, Name, Code
            FROM DOCUMENT_TYPES
            WHERE NVL(IsDeleted, 0) = 0
              AND NVL(IS_ACTIVE, 1) = 1
            ORDER BY Name ASC";
        return await _connection.QueryAsync<ReportDossierLookupItem>(sql);
    }

    public async Task<IEnumerable<ReportDossierLookupItem>> GetShelvesAsync(long? unitScopeRoot, long? filterUnitId)
    {
        EnsureOpen();
        var parameters = new DynamicParameters();
        var sql = @"
            SELECT CAST(s.Id AS VARCHAR2(50)) AS Id, s.Name, s.Code
            FROM PHYSICAL_SHELF s
            WHERE NVL(s.IS_DELETED, 0) = 0";

        var effectiveUnitId = filterUnitId ?? unitScopeRoot;
        if (effectiveUnitId.HasValue)
        {
            sql += @" AND s.UnitId IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        sql += " ORDER BY s.Name ASC";
        return await _connection.QueryAsync<ReportDossierLookupItem>(sql, parameters);
    }

    public async Task<IEnumerable<ReportDossierLookupItem>> GetFloorsAsync(long? unitScopeRoot, long? filterUnitId)
    {
        EnsureOpen();
        var parameters = new DynamicParameters();
        var sql = @"
            SELECT CAST(f.Id AS VARCHAR2(50)) AS Id, f.Name, f.Code
            FROM PHYSICAL_FLOOR f
            INNER JOIN PHYSICAL_SHELF s ON f.ShelfId = s.Id AND NVL(s.IS_DELETED, 0) = 0
            WHERE NVL(f.IS_DELETED, 0) = 0";

        var effectiveUnitId = filterUnitId ?? unitScopeRoot;
        if (effectiveUnitId.HasValue)
        {
            sql += @" AND s.UnitId IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        sql += " ORDER BY f.Name ASC";
        return await _connection.QueryAsync<ReportDossierLookupItem>(sql, parameters);
    }

    public async Task<IEnumerable<ReportDossierLookupItem>> GetBoxesAsync(long? unitScopeRoot, long? filterUnitId)
    {
        EnsureOpen();
        var parameters = new DynamicParameters();
        var sql = @"
            SELECT CAST(b.Id AS VARCHAR2(50)) AS Id, b.Name, b.Code
            FROM PHYSICAL_BOX b
            INNER JOIN PHYSICAL_FLOOR f ON b.FloorId = f.Id AND NVL(f.IS_DELETED, 0) = 0
            INNER JOIN PHYSICAL_SHELF s ON f.ShelfId = s.Id AND NVL(s.IS_DELETED, 0) = 0
            WHERE NVL(b.IS_DELETED, 0) = 0";

        var effectiveUnitId = filterUnitId ?? unitScopeRoot;
        if (effectiveUnitId.HasValue)
        {
            sql += @" AND s.UnitId IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        sql += " ORDER BY b.Name ASC";
        return await _connection.QueryAsync<ReportDossierLookupItem>(sql, parameters);
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

    public async Task<IEnumerable<int>> GetAvailableYearsAsync()
    {
        EnsureOpen();
        const string sql = @"
            SELECT DISTINCT EXTRACT(YEAR FROM CreatedDate) AS Yr
            FROM DOSSIERS
            WHERE IsDeleted = 0 AND STATUS_ID = 6 AND PUBLISHSTATUSID = 2
            ORDER BY Yr DESC";

        var years = (await _connection.QueryAsync<int?>(sql))
            .Where(y => y.HasValue && y.Value > 2000)
            .Select(y => y!.Value)
            .ToList();

        if (years.Count == 0)
        {
            years.Add(DateTime.Now.Year);
        }

        return years;
    }

    public async Task<IEnumerable<EvnHanoi.ReportService.Core.DTOs.DossierByYearChartStatDto>> GetDossierByYearChartStatsAsync(
        EvnHanoi.ReportService.Core.DTOs.DossierByYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (targetYear, allYears) = ResolveReportYearFilter(filter.Year);
        var parameters = new DynamicParameters();
        if (!allYears)
            parameters.Add("Year", targetYear);

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var sql = @"
            SELECT
                CASE
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN 'STATION'
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN 'LINE'
                    ELSE 'EQUIPMENT'
                END AS GroupCode,
                CASE
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN N'Trạm biến áp'
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN N'Đường dây'
                    ELSE N'Thiết bị'
                END AS GroupName,
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE d.IsDeleted = 0
              AND d.STATUS_ID = 6
              AND d.PUBLISHSTATUSID = 2" + GetReportYearSqlClause(allYears);

        if (effectiveUnitId.HasValue)
        {
            sql += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        if (filter.ObjectType.HasValue && filter.ObjectType.Value > 0)
        {
            if (filter.ObjectType.Value == 1)
                sql += " AND NVL(d.DOSSIER_GROUP_ID, 1) = 1";
            else if (filter.ObjectType.Value == 2)
                sql += " AND NVL(d.DOSSIER_GROUP_ID, 1) = 2";
            else if (filter.ObjectType.Value == 3)
                sql += " AND NVL(d.DOSSIER_GROUP_ID, 1) IN (3, 4)";
        }

        sql += @" GROUP BY
            CASE
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN 'STATION'
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN 'LINE'
                ELSE 'EQUIPMENT'
            END,
            CASE
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN N'Trạm biến áp'
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN N'Đường dây'
                ELSE N'Thiết bị'
            END";

        var rows = (await _connection.QueryAsync<EvnHanoi.ReportService.Core.DTOs.DossierByYearChartStatDto>(sql, parameters)).ToList();

        var defaultGroups = new List<(string Code, string Name)>
        {
            ("STATION", "Trạm biến áp"),
            ("LINE", "Đường dây"),
            ("EQUIPMENT", "Thiết bị")
        };

        if (filter.ObjectType.HasValue && filter.ObjectType.Value > 0)
        {
            if (filter.ObjectType.Value == 1) defaultGroups = defaultGroups.Where(g => g.Code == "STATION").ToList();
            else if (filter.ObjectType.Value == 2) defaultGroups = defaultGroups.Where(g => g.Code == "LINE").ToList();
            else if (filter.ObjectType.Value == 3) defaultGroups = defaultGroups.Where(g => g.Code == "EQUIPMENT").ToList();
        }

        var result = new List<EvnHanoi.ReportService.Core.DTOs.DossierByYearChartStatDto>();
        foreach (var (code, name) in defaultGroups)
        {
            var match = rows.FirstOrDefault(r => string.Equals(r.GroupCode, code, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                result.Add(match);
            }
            else
            {
                result.Add(new EvnHanoi.ReportService.Core.DTOs.DossierByYearChartStatDto
                {
                    GroupCode = code,
                    GroupName = name,
                    DossierCount = 0,
                    DocumentCount = 0,
                    PageCount = 0
                });
            }
        }

        return result;
    }

    public async Task<IEnumerable<EvnHanoi.ReportService.Core.DTOs.DossierByYearRatioStatDto>> GetDossierByYearRatioStatsAsync(
        EvnHanoi.ReportService.Core.DTOs.DossierByYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var chartStats = await GetDossierByYearChartStatsAsync(filter, isAdmin, userUnitId);
        var totalDossiers = chartStats.Sum(s => s.DossierCount);

        return chartStats.Select(s => new EvnHanoi.ReportService.Core.DTOs.DossierByYearRatioStatDto
        {
            GroupCode = s.GroupCode,
            GroupName = s.GroupName,
            DossierCount = s.DossierCount,
            Percentage = totalDossiers > 0 ? Math.Round((decimal)s.DossierCount / totalDossiers * 100, 2) : 0
        });
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByYearListAsync(
        DossierByYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByYearFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsStationGridResponseDto> GetDossierByYearStationGridAsync(
        DossierByYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByYearStationGridFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            WITH filtered AS (
                SELECT d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
                GROUP BY d.InfrastructureId
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
            ),
            infra_stats AS (
                SELECT
                    f.InfrastructureId,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.InfrastructureId
            )
            SELECT
                i.CODE AS InfrastructureCode,
                i.NAME AS InfrastructureName,
                gt.NAME AS GridTypeName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM infra_stats s
            INNER JOIN INFRASTRUCTURE i ON i.ID = s.InfrastructureId
            LEFT JOIN GridTypes gt ON i.GridTypeId = gt.Id
            ORDER BY i.NAME
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsStationGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var infraCode = Convert.ToString(r.INFRASTRUCTURECODE ?? r.InfrastructureCode);
            var infraName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName);
            items.Add(new ReportStatisticsStationGridItemDto
            {
                Stt = stt++,
                GridTypeName = Convert.ToString(r.GRIDTYPENAME ?? r.GridTypeName),
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0),
                CatalogData = BuildInfrastructureCatalogData(infraCode, infraName, bhsColumns)
            });
        }

        return new ReportStatisticsStationGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<ReportMonthLookupDto>> GetAvailableMonthsAsync()
    {
        EnsureOpen();
        const string sql = @"
            SELECT DISTINCT
                EXTRACT(YEAR FROM CreatedDate) AS Yr,
                EXTRACT(MONTH FROM CreatedDate) AS Mo
            FROM DOSSIERS
            WHERE IsDeleted = 0 AND STATUS_ID = 6 AND PUBLISHSTATUSID = 2
            ORDER BY Yr DESC, Mo DESC";

        var rows = (await _connection.QueryAsync(sql)).ToList();
        var result = new List<ReportMonthLookupDto>();

        foreach (var r in rows)
        {
            var year = Convert.ToInt32(r.YR ?? r.Yr ?? 0);
            var month = Convert.ToInt32(r.MO ?? r.Mo ?? 0);
            if (year <= 2000 || month is < 1 or > 12)
                continue;

            result.Add(new ReportMonthLookupDto
            {
                Year = year,
                Month = month,
                Label = $"Tháng {month:D2}/{year}"
            });
        }

        if (result.Count == 0)
        {
            var now = DateTime.Now;
            result.Add(new ReportMonthLookupDto
            {
                Year = now.Year,
                Month = now.Month,
                Label = $"Tháng {now.Month:D2}/{now.Year}"
            });
        }

        return result;
    }

    public async Task<IEnumerable<DossierByMonthChartStatDto>> GetDossierByMonthChartStatsAsync(
        DossierByMonthFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (targetYear, targetMonth, parameters, effectiveUnitId) = BuildDossierByMonthChartParameters(filter, isAdmin, userUnitId);

        var sql = @"
            SELECT
                CASE
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN 'STATION'
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN 'LINE'
                    ELSE 'EQUIPMENT'
                END AS GroupCode,
                CASE
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN N'Trạm biến áp'
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN N'Đường dây'
                    ELSE N'Thiết bị'
                END AS GroupName,
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE d.IsDeleted = 0
              AND d.STATUS_ID = 6
              AND d.PUBLISHSTATUSID = 2
              AND EXTRACT(YEAR FROM d.CreatedDate) = :Year
              AND EXTRACT(MONTH FROM d.CreatedDate) = :Month";

        if (effectiveUnitId.HasValue)
        {
            sql += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendObjectTypeFilter(filter.ObjectType, ref sql);

        sql += @" GROUP BY
            CASE
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN 'STATION'
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN 'LINE'
                ELSE 'EQUIPMENT'
            END,
            CASE
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN N'Trạm biến áp'
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN N'Đường dây'
                ELSE N'Thiết bị'
            END";

        var rows = (await _connection.QueryAsync<DossierByMonthChartStatDto>(sql, parameters)).ToList();
        return FillDefaultChartGroups(rows, filter.ObjectType, (code, name) => new DossierByMonthChartStatDto
        {
            GroupCode = code,
            GroupName = name,
            DossierCount = 0,
            DocumentCount = 0,
            PageCount = 0
        });
    }

    public async Task<IEnumerable<DossierByMonthRatioStatDto>> GetDossierByMonthRatioStatsAsync(
        DossierByMonthFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var chartStats = await GetDossierByMonthChartStatsAsync(filter, isAdmin, userUnitId);
        var totalDossiers = chartStats.Sum(s => s.DossierCount);

        return chartStats.Select(s => new DossierByMonthRatioStatDto
        {
            GroupCode = s.GroupCode,
            GroupName = s.GroupName,
            DossierCount = s.DossierCount,
            Percentage = totalDossiers > 0 ? Math.Round((decimal)s.DossierCount / totalDossiers * 100, 2) : 0
        });
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByMonthListAsync(
        DossierByMonthFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, _, page, pageSize, parameters, baseWhere) = BuildDossierByMonthFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsStationGridResponseDto> GetDossierByMonthStationGridAsync(
        DossierByMonthFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, _, page, pageSize, parameters, baseWhere) = BuildDossierByMonthStationGridFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            WITH filtered AS (
                SELECT d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
                GROUP BY d.InfrastructureId
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
            ),
            infra_stats AS (
                SELECT
                    f.InfrastructureId,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.InfrastructureId
            )
            SELECT
                i.CODE AS InfrastructureCode,
                i.NAME AS InfrastructureName,
                gt.NAME AS GridTypeName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM infra_stats s
            INNER JOIN INFRASTRUCTURE i ON i.ID = s.InfrastructureId
            LEFT JOIN GridTypes gt ON i.GridTypeId = gt.Id
            ORDER BY i.NAME
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsStationGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var infraCode = Convert.ToString(r.INFRASTRUCTURECODE ?? r.InfrastructureCode);
            var infraName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName);
            items.Add(new ReportStatisticsStationGridItemDto
            {
                Stt = stt++,
                GridTypeName = Convert.ToString(r.GRIDTYPENAME ?? r.GridTypeName),
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0),
                CatalogData = BuildInfrastructureCatalogData(infraCode, infraName, bhsColumns)
            });
        }

        return new ReportStatisticsStationGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<ReportInputUserLookupDto>> GetInputUsersAsync(bool isAdmin, long? userUnitId)
    {
        EnsureOpen();
        var parameters = new DynamicParameters();
        var sql = @"
            SELECT DISTINCT
                d.CreatorUsername AS Id,
                NVL(d.CreatorName, d.CreatorUsername) AS Name
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE d.IsDeleted = 0
              AND d.STATUS_ID = 6
              AND d.PUBLISHSTATUSID = 2
              AND NVL(d.KIND_ID, 2) = 2
              AND d.CreatorUsername IS NOT NULL";

        if (!isAdmin && userUnitId.HasValue)
        {
            sql += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", userUnitId.Value);
        }

        sql += " ORDER BY Name";

        return await _connection.QueryAsync<ReportInputUserLookupDto>(sql, parameters);
    }

    public async Task<IEnumerable<DossierByAllocationChartStatDto>> GetDossierByAllocationChartStatsAsync(
        DossierByAllocationFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (targetYear, allYears, parameters, effectiveUnitId) = BuildDossierByAllocationChartParameters(filter, isAdmin, userUnitId);

        var sql = @"
            SELECT
                CASE
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN 'STATION'
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN 'LINE'
                    ELSE 'EQUIPMENT'
                END AS GroupCode,
                CASE
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN N'Trạm biến áp'
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN N'Đường dây'
                    ELSE N'Thiết bị'
                END AS GroupName,
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE d.IsDeleted = 0
              AND d.STATUS_ID = 6
              AND d.PUBLISHSTATUSID = 2
              AND NVL(d.KIND_ID, 2) = 2" + GetReportYearSqlClause(allYears);

        if (effectiveUnitId.HasValue)
        {
            sql += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendObjectTypeFilter(filter.ObjectType, ref sql);
        AppendCreatedByFilter(filter.CreatedBy, ref sql, parameters);

        sql += @" GROUP BY
            CASE
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN 'STATION'
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN 'LINE'
                ELSE 'EQUIPMENT'
            END,
            CASE
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN N'Trạm biến áp'
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN N'Đường dây'
                ELSE N'Thiết bị'
            END";

        var rows = (await _connection.QueryAsync<DossierByAllocationChartStatDto>(sql, parameters)).ToList();
        return FillDefaultChartGroups(rows, filter.ObjectType, (code, name) => new DossierByAllocationChartStatDto
        {
            GroupCode = code,
            GroupName = name,
            DossierCount = 0,
            DocumentCount = 0,
            PageCount = 0
        });
    }

    public async Task<IEnumerable<DossierByAllocationRatioStatDto>> GetDossierByAllocationRatioStatsAsync(
        DossierByAllocationFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var chartStats = await GetDossierByAllocationChartStatsAsync(filter, isAdmin, userUnitId);
        var totalDossiers = chartStats.Sum(s => s.DossierCount);

        return chartStats.Select(s => new DossierByAllocationRatioStatDto
        {
            GroupCode = s.GroupCode,
            GroupName = s.GroupName,
            DossierCount = s.DossierCount,
            Percentage = totalDossiers > 0 ? Math.Round((decimal)s.DossierCount / totalDossiers * 100, 2) : 0
        });
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByAllocationListAsync(
        DossierByAllocationFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByAllocationFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsCreatorGridResponseDto> GetDossierByAllocationCreatorGridAsync(
        DossierByAllocationFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByAllocationFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            WITH filtered AS (
                SELECT d.CreatorUsername
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
                  AND d.CreatorUsername IS NOT NULL
                GROUP BY d.CreatorUsername
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, d.CreatorUsername, d.CreatorName
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
                  AND d.CreatorUsername IS NOT NULL
            ),
            creator_stats AS (
                SELECT
                    f.CreatorUsername,
                    MAX(f.CreatorName) AS CreatorName,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.CreatorUsername
            )
            SELECT
                cs.CreatorUsername AS Username,
                NVL(cs.CreatorName, u.FullName) AS FullName,
                ou.Name AS UnitName,
                cs.TotalDossiers,
                cs.TotalDocuments,
                cs.TotalPages
            FROM creator_stats cs
            LEFT JOIN APP_USER u ON LOWER(TRIM(u.UserName)) = LOWER(TRIM(cs.CreatorUsername))
                AND NVL(u.IsDeleted, 0) = 0
            LEFT JOIN ORGANIZATION_UNIT ou ON u.OrganizationUnitId = ou.Id
                AND NVL(ou.IsDeleted, 0) = 0
            ORDER BY cs.CreatorUsername
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsCreatorGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            items.Add(new ReportStatisticsCreatorGridItemDto
            {
                Stt = stt++,
                Username = Convert.ToString(r.USERNAME ?? r.Username) ?? string.Empty,
                FullName = Convert.ToString(r.FULLNAME ?? r.FullName) ?? string.Empty,
                UnitName = Convert.ToString(r.UNITNAME ?? r.UnitName) ?? string.Empty,
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0)
            });
        }

        return new ReportStatisticsCreatorGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<DossierByVoltageGridChartStatDto>> GetDossierByVoltageGridChartStatsAsync(
        DossierByVoltageGridFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (targetYear, allYears) = ResolveReportYearFilter(filter.Year);
        var parameters = new DynamicParameters();
        if (!allYears)
            parameters.Add("Year", targetYear);

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var sql = @"
            SELECT
                CASE
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN 'STATION'
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN 'LINE'
                    ELSE 'EQUIPMENT'
                END AS GroupCode,
                CASE
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN N'Trạm biến áp'
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN N'Đường dây'
                    ELSE N'Thiết bị'
                END AS GroupName,
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE d.IsDeleted = 0
              AND d.STATUS_ID = 6
              AND d.PUBLISHSTATUSID = 2" + GetReportYearSqlClause(allYears);

        if (effectiveUnitId.HasValue)
        {
            sql += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendObjectTypeFilter(filter.ObjectType, ref sql);
        AppendGridTypeFilter(filter.GridTypeId, ref sql, parameters);

        sql += @" GROUP BY
            CASE
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN 'STATION'
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN 'LINE'
                ELSE 'EQUIPMENT'
            END,
            CASE
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN N'Trạm biến áp'
                WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN N'Đường dây'
                ELSE N'Thiết bị'
            END";

        var rows = (await _connection.QueryAsync<DossierByVoltageGridChartStatDto>(sql, parameters)).ToList();
        return FillDefaultChartGroups(rows, filter.ObjectType, (code, name) => new DossierByVoltageGridChartStatDto
        {
            GroupCode = code,
            GroupName = name,
            DossierCount = 0,
            DocumentCount = 0,
            PageCount = 0
        });
    }

    public async Task<IEnumerable<DossierByVoltageGridRatioStatDto>> GetDossierByVoltageGridRatioStatsAsync(
        DossierByVoltageGridFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var chartStats = await GetDossierByVoltageGridChartStatsAsync(filter, isAdmin, userUnitId);
        var totalDossiers = chartStats.Sum(s => s.DossierCount);

        return chartStats.Select(s => new DossierByVoltageGridRatioStatDto
        {
            GroupCode = s.GroupCode,
            GroupName = s.GroupName,
            DossierCount = s.DossierCount,
            Percentage = totalDossiers > 0 ? Math.Round((decimal)s.DossierCount / totalDossiers * 100, 2) : 0
        });
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByVoltageGridListAsync(
        DossierByVoltageGridFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByVoltageGridFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsStationGridResponseDto> GetDossierByVoltageGridStationGridAsync(
        DossierByVoltageGridFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByVoltageGridStationGridFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            WITH filtered AS (
                SELECT d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
                GROUP BY d.InfrastructureId
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
            ),
            infra_stats AS (
                SELECT
                    f.InfrastructureId,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.InfrastructureId
            )
            SELECT
                i.CODE AS InfrastructureCode,
                i.NAME AS InfrastructureName,
                gt.NAME AS GridTypeName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM infra_stats s
            INNER JOIN INFRASTRUCTURE i ON i.ID = s.InfrastructureId
            LEFT JOIN GridTypes gt ON i.GridTypeId = gt.Id
            ORDER BY i.NAME
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsStationGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var infraCode = Convert.ToString(r.INFRASTRUCTURECODE ?? r.InfrastructureCode);
            var infraName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName);
            items.Add(new ReportStatisticsStationGridItemDto
            {
                Stt = stt++,
                GridTypeName = Convert.ToString(r.GRIDTYPENAME ?? r.GridTypeName),
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0),
                CatalogData = BuildInfrastructureCatalogData(infraCode, infraName, bhsColumns)
            });
        }

        return new ReportStatisticsStationGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Filter lưới theo trạm/đường dây — cùng điều kiện hồ sơ với chart/list theo lưới điện áp.
    /// </summary>
    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByVoltageGridStationGridFilter(
        DossierByVoltageGridFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, page, pageSize, parameters, baseWhere) =
            BuildDossierByVoltageGridFilter(filter, isAdmin, userUnitId);

        baseWhere += " AND NVL(i.IsDeleted, 0) = 0";

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByVoltageGridFilter(
        DossierByVoltageGridFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears) = ResolveReportYearFilter(filter.Year);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendObjectTypeFilterToWhere(filter.ObjectType, ref baseWhere);
        AppendGridTypeFilterToWhere(filter.GridTypeId, ref baseWhere, parameters);
        AppendReportYearFilterToWhere(allYears, targetYear, ref baseWhere, parameters);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static void AppendGridTypeFilter(int? gridTypeId, ref string sql, DynamicParameters parameters)
    {
        if (!gridTypeId.HasValue || gridTypeId.Value <= 0)
            return;

        sql += " AND NVL(i.GridTypeId, d.GridTypeId) = :GridTypeId";
        parameters.Add("GridTypeId", gridTypeId.Value);
    }

    private static void AppendGridTypeFilterToWhere(int? gridTypeId, ref string baseWhere, DynamicParameters parameters)
    {
        if (!gridTypeId.HasValue || gridTypeId.Value <= 0)
            return;

        baseWhere += " AND NVL(i.GridTypeId, d.GridTypeId) = :GridTypeId";
        parameters.Add("GridTypeId", gridTypeId.Value);
    }

    /// <summary>
    /// Filter lưới theo trạm/đường dây — cùng điều kiện hồ sơ với chart/list theo tháng.
    /// </summary>
    private static (int Year, int Month, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByMonthStationGridFilter(
        DossierByMonthFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (year, month, page, pageSize, parameters, baseWhere) =
            BuildDossierByMonthFilter(filter, isAdmin, userUnitId);

        baseWhere += " AND NVL(i.IsDeleted, 0) = 0";

        return (year, month, page, pageSize, parameters, baseWhere);
    }

    private static (int Year, int Month, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByMonthFilter(
        DossierByMonthFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var targetYear = filter.Year.HasValue && filter.Year.Value > 0 ? filter.Year.Value : DateTime.Now.Year;
        var targetMonth = filter.Month.HasValue && filter.Month.Value is >= 1 and <= 12
            ? filter.Month.Value
            : DateTime.Now.Month;
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();
        parameters.Add("Year", targetYear);
        parameters.Add("Month", targetMonth);

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND EXTRACT(YEAR FROM d.CreatedDate) = :Year
            AND EXTRACT(MONTH FROM d.CreatedDate) = :Month";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendObjectTypeFilterToWhere(filter.ObjectType, ref baseWhere);

        return (targetYear, targetMonth, page, pageSize, parameters, baseWhere);
    }

    private static (int Year, int Month, DynamicParameters Parameters, long? EffectiveUnitId) BuildDossierByMonthChartParameters(
        DossierByMonthFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var targetYear = filter.Year.HasValue && filter.Year.Value > 0 ? filter.Year.Value : DateTime.Now.Year;
        var targetMonth = filter.Month.HasValue && filter.Month.Value is >= 1 and <= 12
            ? filter.Month.Value
            : DateTime.Now.Month;

        var parameters = new DynamicParameters();
        parameters.Add("Year", targetYear);
        parameters.Add("Month", targetMonth);

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);
        return (targetYear, targetMonth, parameters, effectiveUnitId);
    }

    private static void AppendObjectTypeFilter(int? objectType, ref string sql)
    {
        if (!objectType.HasValue || objectType.Value <= 0)
            return;

        if (objectType.Value == 1)
            sql += " AND NVL(d.DOSSIER_GROUP_ID, 1) = 1";
        else if (objectType.Value == 2)
            sql += " AND NVL(d.DOSSIER_GROUP_ID, 1) = 2";
        else if (objectType.Value == 3)
            sql += " AND NVL(d.DOSSIER_GROUP_ID, 1) IN (3, 4)";
    }

    private static void AppendObjectTypeFilterToWhere(int? objectType, ref string baseWhere)
    {
        if (!objectType.HasValue || objectType.Value <= 0)
            return;

        if (objectType.Value == 1)
            baseWhere += " AND NVL(d.DOSSIER_GROUP_ID, 1) = 1";
        else if (objectType.Value == 2)
            baseWhere += " AND NVL(d.DOSSIER_GROUP_ID, 1) = 2";
        else if (objectType.Value == 3)
            baseWhere += " AND NVL(d.DOSSIER_GROUP_ID, 1) IN (3, 4)";
    }

    private static void AppendCreatedByFilter(string? createdBy, ref string sql, DynamicParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(createdBy))
            return;

        sql += " AND d.CreatorUsername = :CreatedBy";
        parameters.Add("CreatedBy", createdBy.Trim());
    }

    private static void AppendCreatedByFilterToWhere(string? createdBy, ref string baseWhere, DynamicParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(createdBy))
            return;

        baseWhere += " AND d.CreatorUsername = :CreatedBy";
        parameters.Add("CreatedBy", createdBy.Trim());
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByAllocationFilter(
        DossierByAllocationFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears) = ResolveReportYearFilter(filter.Year);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND NVL(d.KIND_ID, 2) = 2";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendObjectTypeFilterToWhere(filter.ObjectType, ref baseWhere);
        AppendCreatedByFilterToWhere(filter.CreatedBy, ref baseWhere, parameters);
        AppendReportYearFilterToWhere(allYears, targetYear, ref baseWhere, parameters);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static (int Year, bool AllYears, DynamicParameters Parameters, long? EffectiveUnitId) BuildDossierByAllocationChartParameters(
        DossierByAllocationFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears) = ResolveReportYearFilter(filter.Year);
        var parameters = new DynamicParameters();
        if (!allYears)
            parameters.Add("Year", targetYear);

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);
        return (targetYear, allYears, parameters, effectiveUnitId);
    }

    private static List<TChart> FillDefaultChartGroups<TChart>(
        List<TChart> rows,
        int? objectType,
        Func<string, string, TChart> emptyFactory)
        where TChart : DossierByMonthChartStatDto
    {
        var defaultGroups = new List<(string Code, string Name)>
        {
            ("STATION", "Trạm biến áp"),
            ("LINE", "Đường dây"),
            ("EQUIPMENT", "Thiết bị")
        };

        if (objectType.HasValue && objectType.Value > 0)
        {
            if (objectType.Value == 1) defaultGroups = defaultGroups.Where(g => g.Code == "STATION").ToList();
            else if (objectType.Value == 2) defaultGroups = defaultGroups.Where(g => g.Code == "LINE").ToList();
            else if (objectType.Value == 3) defaultGroups = defaultGroups.Where(g => g.Code == "EQUIPMENT").ToList();
        }

        var result = new List<TChart>();
        foreach (var (code, name) in defaultGroups)
        {
            var match = rows.FirstOrDefault(r => string.Equals(r.GroupCode, code, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                result.Add(match);
            else
                result.Add(emptyFactory(code, name));
        }

        return result;
    }

    /// <summary>
    /// Filter lưới theo trạm/đường dây — cùng điều kiện hồ sơ với chart/list (DOSSIER_GROUP_ID), chỉ khác bước gom theo infrastructure.
    /// </summary>
    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByYearStationGridFilter(
        DossierByYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, page, pageSize, parameters, baseWhere) =
            BuildDossierByYearFilter(filter, isAdmin, userUnitId);

        baseWhere += " AND NVL(i.IsDeleted, 0) = 0";

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByYearFilter(
        DossierByYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears) = ResolveReportYearFilter(filter.Year);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        if (filter.ObjectType.HasValue && filter.ObjectType.Value > 0)
        {
            if (filter.ObjectType.Value == 1)
                baseWhere += " AND NVL(d.DOSSIER_GROUP_ID, 1) = 1";
            else if (filter.ObjectType.Value == 2)
                baseWhere += " AND NVL(d.DOSSIER_GROUP_ID, 1) = 2";
            else if (filter.ObjectType.Value == 3)
                baseWhere += " AND NVL(d.DOSSIER_GROUP_ID, 1) IN (3, 4)";
        }

        AppendReportYearFilterToWhere(allYears, targetYear, ref baseWhere, parameters);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static Dictionary<string, string> ParseBhsCatalogData(
        string? formDataJson,
        IReadOnlyList<ReportDossierBhsColumn> bhsColumns)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(formDataJson))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(formDataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var catalog in bhsColumns)
            {
                if (!doc.RootElement.TryGetProperty(catalog.Code, out var prop) &&
                    !doc.RootElement.TryGetProperty(catalog.Key, out prop) &&
                    !doc.RootElement.TryGetProperty(catalog.Label, out prop))
                {
                    continue;
                }

                var val = prop.ValueKind switch
                {
                    JsonValueKind.String => prop.GetString() ?? string.Empty,
                    JsonValueKind.Number => prop.GetRawText(),
                    JsonValueKind.True or JsonValueKind.False => prop.GetBoolean().ToString(),
                    JsonValueKind.Null => string.Empty,
                    _ => prop.GetRawText()
                };

                if (!string.IsNullOrWhiteSpace(val))
                    result[catalog.Key] = val;
            }
        }
        catch
        {
            // Bỏ qua lỗi cú pháp JSON
        }

        return result;
    }

    /// <summary>
    /// View Lưới theo trạm: cột BHS hiển thị mã/tên infrastructure, không lấy FormDataJson hồ sơ.
    /// </summary>
    private static Dictionary<string, string> BuildInfrastructureCatalogData(
        string? infrastructureCode,
        string? infrastructureName,
        IReadOnlyList<ReportDossierBhsColumn> bhsColumns)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ordered = bhsColumns.OrderBy(c => c.Priority).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var col = ordered[i];
            var value = ResolveInfrastructureCatalogValue(col, i, ordered.Count, infrastructureCode, infrastructureName);
            if (!string.IsNullOrWhiteSpace(value))
                result[col.Key] = value;
        }

        return result;
    }

    private static string? ResolveInfrastructureCatalogValue(
        ReportDossierBhsColumn col,
        int index,
        int totalColumns,
        string? infrastructureCode,
        string? infrastructureName)
    {
        var hint = $"{col.Label} {col.Code} {col.Key}".ToLowerInvariant();

        if (hint.Contains("mã") || hint.Contains("code"))
            return infrastructureCode;

        if (hint.Contains("tên") || hint.Contains("name"))
            return infrastructureName;

        // Mặc định: cột BHS đầu = mã trạm/ĐZ, cột thứ hai = tên trạm/ĐZ
        if (index == 0)
            return infrastructureCode;

        if (index == 1 && totalColumns > 1)
            return infrastructureName;

        return null;
    }

    public async Task<IEnumerable<DossierByEquipmentTypeChartStatDto>> GetDossierByEquipmentTypeChartStatsAsync(
        DossierByEquipmentTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (targetYear, parameters, baseWhere) = BuildDossierByEquipmentTypeChartParameters(filter, isAdmin, userUnitId);
        var equipmentTypeClause = BuildEquipmentTypeChartDimensionClause(filter.EquipmentTypeIds);

        var sql = $@"
            SELECT
                et.CODE AS EquipmentTypeCode,
                et.NAME AS EquipmentTypeName,
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
            INNER JOIN Equipments e ON de.EquipmentId = e.Id AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
            INNER JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}{equipmentTypeClause}
            GROUP BY et.ID, et.CODE, et.NAME
            ORDER BY et.NAME";

        return await _connection.QueryAsync<DossierByEquipmentTypeChartStatDto>(sql, parameters);
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByEquipmentTypeListAsync(
        DossierByEquipmentTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByEquipmentTypeFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsEquipmentTypeGridResponseDto> GetDossierByEquipmentTypeGridAsync(
        DossierByEquipmentTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByEquipmentTypeGridFilter(filter, isAdmin, userUnitId);
        var equipmentTypeClause = BuildEquipmentTypeChartDimensionClause(filter.EquipmentTypeIds);

        var countSql = $@"
            WITH filtered AS (
                SELECT DISTINCT et.ID AS EquipmentTypeId, NVL(i.GridTypeId, d.GridTypeId) AS GridTypeId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
                INNER JOIN Equipments e ON de.EquipmentId = e.Id AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
                INNER JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
                WHERE {baseWhere}{equipmentTypeClause}
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, et.ID AS EquipmentTypeId, et.CODE AS EquipmentTypeCode,
                       et.NAME AS EquipmentTypeName, NVL(i.GridTypeId, d.GridTypeId) AS GridTypeId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
                INNER JOIN Equipments e ON de.EquipmentId = e.Id AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
                INNER JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
                WHERE {baseWhere}{equipmentTypeClause}
            ),
            type_stats AS (
                SELECT
                    f.EquipmentTypeId,
                    f.EquipmentTypeCode,
                    f.EquipmentTypeName,
                    f.GridTypeId,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.EquipmentTypeId, f.EquipmentTypeCode, f.EquipmentTypeName, f.GridTypeId
            )
            SELECT
                s.EquipmentTypeCode,
                s.EquipmentTypeName,
                gt.NAME AS GridTypeName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM type_stats s
            LEFT JOIN GridTypes gt ON s.GridTypeId = gt.Id
            ORDER BY s.EquipmentTypeName, gt.Name
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsEquipmentTypeGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            items.Add(new ReportStatisticsEquipmentTypeGridItemDto
            {
                Stt = stt++,
                EquipmentTypeCode = Convert.ToString(r.EQUIPMENTTYPECODE ?? r.EquipmentTypeCode) ?? "-",
                EquipmentTypeName = Convert.ToString(r.EQUIPMENTTYPENAME ?? r.EquipmentTypeName) ?? "-",
                GridTypeName = Convert.ToString(r.GRIDTYPENAME ?? r.GridTypeName) ?? "-",
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0)
            });
        }

        return new ReportStatisticsEquipmentTypeGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static (int Year, DynamicParameters Parameters, string BaseWhere) BuildDossierByEquipmentTypeChartParameters(
        DossierByEquipmentTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, _, _, parameters, baseWhere) =
            BuildDossierByEquipmentTypeFilter(filter, isAdmin, userUnitId);
        return (targetYear, parameters, baseWhere);
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByEquipmentTypeGridFilter(
        DossierByEquipmentTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, page, pageSize, parameters, baseWhere) =
            BuildDossierByEquipmentTypeFilter(filter, isAdmin, userUnitId);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByEquipmentTypeFilter(
        DossierByEquipmentTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears) = ResolveReportYearFilter(filter.Year);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendObjectTypeFilterToWhere(filter.ObjectType, ref baseWhere);
        AppendEquipmentTypeFilterToWhere(filter.EquipmentTypeIds, ref baseWhere, parameters);
        AppendReportYearFilterToWhere(allYears, targetYear, ref baseWhere, parameters);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static List<string> NormalizeEquipmentTypeIds(IEnumerable<string>? equipmentTypeIds)
    {
        if (equipmentTypeIds == null)
            return new List<string>();

        return equipmentTypeIds
            .SelectMany(id => (id ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendEquipmentTypeFilterToWhere(
        IEnumerable<string>? equipmentTypeIds,
        ref string baseWhere,
        DynamicParameters parameters)
    {
        var ids = NormalizeEquipmentTypeIds(equipmentTypeIds);

        // Báo cáo theo loại thiết bị: luôn chỉ lấy hồ sơ có ít nhất một thiết bị liên kết
        // (chart/lưới dùng INNER JOIN — list phải cùng phạm vi để số liệu khớp).
        baseWhere += @" AND EXISTS (
            SELECT 1
            FROM DOSSIER_EQUIPMENTS de_f
            INNER JOIN Equipments e_f ON de_f.EquipmentId = e_f.Id
            WHERE de_f.DossierId = d.Id
              AND (e_f.IsDeleted = 0 OR e_f.IsDeleted IS NULL)";

        if (ids.Count > 0)
        {
            baseWhere += @"
              AND e_f.EquipmentTypeId IN :EquipmentTypeIds";
            parameters.Add("EquipmentTypeIds", ids.ToArray());
        }

        baseWhere += @"
        )";
    }

    private static string BuildEquipmentTypeChartDimensionClause(IEnumerable<string>? equipmentTypeIds)
    {
        return NormalizeEquipmentTypeIds(equipmentTypeIds).Count > 0
            ? " AND et.Id IN :EquipmentTypeIds"
            : string.Empty;
    }

    public async Task<IEnumerable<DossierByShelfChartStatDto>> GetDossierByShelfChartStatsAsync(
        DossierByShelfFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, parameters, baseWhere) = BuildDossierByShelfChartParameters(filter, isAdmin, userUnitId);
        var shelfClause = BuildShelfChartDimensionClause(filter.ShelfIds);

        var sql = $@"
            SELECT
                shel.CODE AS ShelfCode,
                shel.NAME AS ShelfName,
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN PHYSICAL_SHELF shel ON d.ShelfId = shel.Id AND NVL(shel.IS_DELETED, 0) = 0
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}{shelfClause}
            GROUP BY shel.Id, shel.CODE, shel.NAME
            ORDER BY shel.NAME";

        return await _connection.QueryAsync<DossierByShelfChartStatDto>(sql, parameters);
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByShelfListAsync(
        DossierByShelfFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByShelfFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsShelfGridResponseDto> GetDossierByShelfGridAsync(
        DossierByShelfFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByShelfGridFilter(filter, isAdmin, userUnitId);
        var shelfClause = BuildShelfChartDimensionClause(filter.ShelfIds);

        var countSql = $@"
            WITH filtered AS (
                SELECT DISTINCT shel.Id AS ShelfId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN PHYSICAL_SHELF shel ON d.ShelfId = shel.Id AND NVL(shel.IS_DELETED, 0) = 0
                WHERE {baseWhere}{shelfClause}
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, shel.Id AS ShelfId, shel.CODE AS ShelfCode, shel.NAME AS ShelfName
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN PHYSICAL_SHELF shel ON d.ShelfId = shel.Id AND NVL(shel.IS_DELETED, 0) = 0
                WHERE {baseWhere}{shelfClause}
            ),
            shelf_stats AS (
                SELECT
                    f.ShelfId,
                    f.ShelfCode,
                    f.ShelfName,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.ShelfId, f.ShelfCode, f.ShelfName
            )
            SELECT
                s.ShelfCode,
                s.ShelfName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM shelf_stats s
            ORDER BY s.ShelfName
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsShelfGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            items.Add(new ReportStatisticsShelfGridItemDto
            {
                Stt = stt++,
                ShelfCode = Convert.ToString(r.SHELFCODE ?? r.ShelfCode) ?? "-",
                ShelfName = Convert.ToString(r.SHELFNAME ?? r.ShelfName) ?? "-",
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0)
            });
        }

        return new ReportStatisticsShelfGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static (int Year, DynamicParameters Parameters, string BaseWhere) BuildDossierByShelfChartParameters(
        DossierByShelfFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, _, _, parameters, baseWhere) =
            BuildDossierByShelfFilter(filter, isAdmin, userUnitId);
        return (targetYear, parameters, baseWhere);
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByShelfGridFilter(
        DossierByShelfFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, page, pageSize, parameters, baseWhere) =
            BuildDossierByShelfFilter(filter, isAdmin, userUnitId);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByShelfFilter(
        DossierByShelfFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears) = ResolveReportYearFilter(filter.Year);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND d.ShelfId IS NOT NULL";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendShelfFilterToWhere(filter.ShelfIds, ref baseWhere, parameters);
        AppendReportYearFilterToWhere(allYears, targetYear, ref baseWhere, parameters);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static List<long> NormalizeShelfIds(IEnumerable<string>? shelfIds)
    {
        if (shelfIds == null)
            return new List<long>();

        return shelfIds
            .SelectMany(id => (id ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(id => long.TryParse(id, out var parsed) ? parsed : 0L)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private static void AppendShelfFilterToWhere(
        IEnumerable<string>? shelfIds,
        ref string baseWhere,
        DynamicParameters parameters)
    {
        var ids = NormalizeShelfIds(shelfIds);
        if (ids.Count == 0)
            return;

        baseWhere += " AND d.ShelfId IN :ShelfIds";
        parameters.Add("ShelfIds", ids.ToArray());
    }

    private static string BuildShelfChartDimensionClause(IEnumerable<string>? shelfIds)
    {
        return NormalizeShelfIds(shelfIds).Count > 0
            ? " AND shel.Id IN :ShelfIds"
            : string.Empty;
    }

    public async Task<IEnumerable<DossierByBoxChartStatDto>> GetDossierByBoxChartStatsAsync(
        DossierByBoxFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, parameters, baseWhere) = BuildDossierByBoxChartParameters(filter, isAdmin, userUnitId);
        var boxClause = BuildBoxChartDimensionClause(filter.BoxIds);

        var sql = $@"
            SELECT
                bx.CODE AS BoxCode,
                bx.NAME AS BoxName,
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN PHYSICAL_BOX bx ON d.BoxId = bx.Id AND NVL(bx.IS_DELETED, 0) = 0
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}{boxClause}
            GROUP BY bx.Id, bx.CODE, bx.NAME
            ORDER BY bx.NAME";

        return await _connection.QueryAsync<DossierByBoxChartStatDto>(sql, parameters);
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByBoxListAsync(
        DossierByBoxFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByBoxFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsBoxGridResponseDto> GetDossierByBoxGridAsync(
        DossierByBoxFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByBoxGridFilter(filter, isAdmin, userUnitId);
        var boxClause = BuildBoxChartDimensionClause(filter.BoxIds);

        var countSql = $@"
            WITH filtered AS (
                SELECT DISTINCT bx.Id AS BoxId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN PHYSICAL_BOX bx ON d.BoxId = bx.Id AND NVL(bx.IS_DELETED, 0) = 0
                WHERE {baseWhere}{boxClause}
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, bx.Id AS BoxId, bx.CODE AS BoxCode, bx.NAME AS BoxName
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN PHYSICAL_BOX bx ON d.BoxId = bx.Id AND NVL(bx.IS_DELETED, 0) = 0
                WHERE {baseWhere}{boxClause}
            ),
            box_stats AS (
                SELECT
                    f.BoxId,
                    f.BoxCode,
                    f.BoxName,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.BoxId, f.BoxCode, f.BoxName
            )
            SELECT
                s.BoxCode,
                s.BoxName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM box_stats s
            ORDER BY s.BoxName
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsBoxGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            items.Add(new ReportStatisticsBoxGridItemDto
            {
                Stt = stt++,
                BoxCode = Convert.ToString(r.BOXCODE ?? r.BoxCode) ?? "-",
                BoxName = Convert.ToString(r.BOXNAME ?? r.BoxName) ?? "-",
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0)
            });
        }

        return new ReportStatisticsBoxGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static (int Year, DynamicParameters Parameters, string BaseWhere) BuildDossierByBoxChartParameters(
        DossierByBoxFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, _, _, parameters, baseWhere) =
            BuildDossierByBoxFilter(filter, isAdmin, userUnitId);
        return (targetYear, parameters, baseWhere);
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByBoxGridFilter(
        DossierByBoxFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, page, pageSize, parameters, baseWhere) =
            BuildDossierByBoxFilter(filter, isAdmin, userUnitId);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByBoxFilter(
        DossierByBoxFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears) = ResolveReportYearFilter(filter.Year);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND d.BoxId IS NOT NULL";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendBoxFilterToWhere(filter.BoxIds, ref baseWhere, parameters);
        AppendReportYearFilterToWhere(allYears, targetYear, ref baseWhere, parameters);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static List<long> NormalizeBoxIds(IEnumerable<string>? boxIds)
    {
        if (boxIds == null)
            return new List<long>();

        return boxIds
            .SelectMany(id => (id ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(id => long.TryParse(id, out var parsed) ? parsed : 0L)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private static void AppendBoxFilterToWhere(
        IEnumerable<string>? boxIds,
        ref string baseWhere,
        DynamicParameters parameters)
    {
        var ids = NormalizeBoxIds(boxIds);
        if (ids.Count == 0)
            return;

        baseWhere += " AND d.BoxId IN :BoxIds";
        parameters.Add("BoxIds", ids.ToArray());
    }

    private static string BuildBoxChartDimensionClause(IEnumerable<string>? boxIds)
    {
        return NormalizeBoxIds(boxIds).Count > 0
            ? " AND bx.Id IN :BoxIds"
            : string.Empty;
    }

    public async Task<IEnumerable<DossierByFloorChartStatDto>> GetDossierByFloorChartStatsAsync(
        DossierByFloorFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, parameters, baseWhere) = BuildDossierByFloorChartParameters(filter, isAdmin, userUnitId);
        var floorClause = BuildFloorChartDimensionClause(filter.FloorIds);

        var sql = $@"
            SELECT
                fl.CODE AS FloorCode,
                fl.NAME AS FloorName,
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN PHYSICAL_FLOOR fl ON d.FloorId = fl.Id AND NVL(fl.IS_DELETED, 0) = 0
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}{floorClause}
            GROUP BY fl.Id, fl.CODE, fl.NAME
            ORDER BY fl.NAME";

        return await _connection.QueryAsync<DossierByFloorChartStatDto>(sql, parameters);
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByFloorListAsync(
        DossierByFloorFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByFloorFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsFloorGridResponseDto> GetDossierByFloorGridAsync(
        DossierByFloorFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByFloorGridFilter(filter, isAdmin, userUnitId);
        var floorClause = BuildFloorChartDimensionClause(filter.FloorIds);

        var countSql = $@"
            WITH filtered AS (
                SELECT DISTINCT fl.Id AS FloorId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN PHYSICAL_FLOOR fl ON d.FloorId = fl.Id AND NVL(fl.IS_DELETED, 0) = 0
                WHERE {baseWhere}{floorClause}
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, fl.Id AS FloorId, fl.CODE AS FloorCode, fl.NAME AS FloorName
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN PHYSICAL_FLOOR fl ON d.FloorId = fl.Id AND NVL(fl.IS_DELETED, 0) = 0
                WHERE {baseWhere}{floorClause}
            ),
            floor_stats AS (
                SELECT
                    f.FloorId,
                    f.FloorCode,
                    f.FloorName,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.FloorId, f.FloorCode, f.FloorName
            )
            SELECT
                s.FloorCode,
                s.FloorName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM floor_stats s
            ORDER BY s.FloorName
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsFloorGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            items.Add(new ReportStatisticsFloorGridItemDto
            {
                Stt = stt++,
                FloorCode = Convert.ToString(r.FLOORCODE ?? r.FloorCode) ?? "-",
                FloorName = Convert.ToString(r.FLOORNAME ?? r.FloorName) ?? "-",
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0)
            });
        }

        return new ReportStatisticsFloorGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static (int Year, DynamicParameters Parameters, string BaseWhere) BuildDossierByFloorChartParameters(
        DossierByFloorFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, _, _, parameters, baseWhere) =
            BuildDossierByFloorFilter(filter, isAdmin, userUnitId);
        return (targetYear, parameters, baseWhere);
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByFloorGridFilter(
        DossierByFloorFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, page, pageSize, parameters, baseWhere) =
            BuildDossierByFloorFilter(filter, isAdmin, userUnitId);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByFloorFilter(
        DossierByFloorFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears) = ResolveReportYearFilter(filter.Year);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND d.FloorId IS NOT NULL";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendFloorFilterToWhere(filter.FloorIds, ref baseWhere, parameters);
        AppendReportYearFilterToWhere(allYears, targetYear, ref baseWhere, parameters);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static List<long> NormalizeFloorIds(IEnumerable<string>? floorIds)
    {
        if (floorIds == null)
            return new List<long>();

        return floorIds
            .SelectMany(id => (id ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(id => long.TryParse(id, out var parsed) ? parsed : 0L)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private static void AppendFloorFilterToWhere(
        IEnumerable<string>? floorIds,
        ref string baseWhere,
        DynamicParameters parameters)
    {
        var ids = NormalizeFloorIds(floorIds);
        if (ids.Count == 0)
            return;

        baseWhere += " AND d.FloorId IN :FloorIds";
        parameters.Add("FloorIds", ids.ToArray());
    }

    private static string BuildFloorChartDimensionClause(IEnumerable<string>? floorIds)
    {
        return NormalizeFloorIds(floorIds).Count > 0
            ? " AND fl.Id IN :FloorIds"
            : string.Empty;
    }

    public async Task<IEnumerable<DossierByDossierTypeChartStatDto>> GetDossierByDossierTypeChartStatsAsync(
        DossierByDossierTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, parameters, baseWhere) = BuildDossierByDossierTypeChartParameters(filter, isAdmin, userUnitId);
        var dossierTypeClause = BuildDossierTypeChartDimensionClause(filter.DossierTypeIds);

        var sql = $@"
            SELECT
                dt.CODE AS DossierTypeCode,
                dt.NAME AS DossierTypeName,
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID AND dt.IsDeleted = 0
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}{dossierTypeClause}
            GROUP BY dt.ID, dt.CODE, dt.NAME
            ORDER BY dt.NAME";

        return await _connection.QueryAsync<DossierByDossierTypeChartStatDto>(sql, parameters);
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByDossierTypeListAsync(
        DossierByDossierTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByDossierTypeFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsDossierTypeGridResponseDto> GetDossierByDossierTypeGridAsync(
        DossierByDossierTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByDossierTypeFilter(filter, isAdmin, userUnitId);
        var dossierTypeClause = BuildDossierTypeChartDimensionClause(filter.DossierTypeIds);

        var countSql = $@"
            WITH filtered AS (
                SELECT DISTINCT dt.ID AS DossierTypeId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID AND dt.IsDeleted = 0
                WHERE {baseWhere}{dossierTypeClause}
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, dt.ID AS DossierTypeId, dt.CODE AS DossierTypeCode,
                       dt.NAME AS DossierTypeName
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID AND dt.IsDeleted = 0
                WHERE {baseWhere}{dossierTypeClause}
            ),
            type_stats AS (
                SELECT
                    f.DossierTypeId,
                    f.DossierTypeCode,
                    f.DossierTypeName,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.DossierTypeId, f.DossierTypeCode, f.DossierTypeName
            )
            SELECT
                s.DossierTypeCode,
                s.DossierTypeName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM type_stats s
            ORDER BY s.DossierTypeName
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierTypeGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            items.Add(new ReportStatisticsDossierTypeGridItemDto
            {
                Stt = stt++,
                DossierTypeCode = Convert.ToString(r.DOSSIERTYPECODE ?? r.DossierTypeCode) ?? "-",
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName) ?? "-",
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0)
            });
        }

        return new ReportStatisticsDossierTypeGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static (int Year, DynamicParameters Parameters, string BaseWhere) BuildDossierByDossierTypeChartParameters(
        DossierByDossierTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, _, _, parameters, baseWhere) =
            BuildDossierByDossierTypeFilter(filter, isAdmin, userUnitId);
        return (targetYear, parameters, baseWhere);
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByDossierTypeFilter(
        DossierByDossierTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears) = ResolveReportYearFilter(filter.Year);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendDossierTypeFilterToWhere(filter.DossierTypeIds, ref baseWhere, parameters);
        AppendReportYearFilterToWhere(allYears, targetYear, ref baseWhere, parameters);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static List<string> NormalizeDossierTypeIds(IEnumerable<string>? dossierTypeIds)
    {
        if (dossierTypeIds == null)
            return new List<string>();

        return dossierTypeIds
            .SelectMany(id => (id ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendDossierTypeFilterToWhere(
        IEnumerable<string>? dossierTypeIds,
        ref string baseWhere,
        DynamicParameters parameters)
    {
        var ids = NormalizeDossierTypeIds(dossierTypeIds);
        if (ids.Count == 0)
            return;

        baseWhere += " AND d.DossierTypeId IN :DossierTypeIds";
        parameters.Add("DossierTypeIds", ids.ToArray());
    }

    private static string BuildDossierTypeChartDimensionClause(IEnumerable<string>? dossierTypeIds)
    {
        return NormalizeDossierTypeIds(dossierTypeIds).Count > 0
            ? " AND dt.ID IN :DossierTypeIds"
            : string.Empty;
    }

    public async Task<IEnumerable<DossierByDocumentTypeChartStatDto>> GetDossierByDocumentTypeChartStatsAsync(
        DossierByDocumentTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, parameters, baseWhere) = BuildDossierByDocumentTypeChartParameters(filter, isAdmin, userUnitId);
        var documentTypeClause = BuildDocumentTypeChartDimensionClause(filter.DocumentTypeIds);

        var sql = $@"
            SELECT
                dt.CODE AS DocumentTypeCode,
                dt.NAME AS DocumentTypeName,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            INNER JOIN DOCUMENT_TYPES dt ON doc.DOCUMENT_TYPE_ID = dt.ID AND NVL(dt.IsDeleted, 0) = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}{documentTypeClause}
            GROUP BY dt.ID, dt.CODE, dt.NAME
            ORDER BY dt.NAME";

        return await _connection.QueryAsync<DossierByDocumentTypeChartStatDto>(sql, parameters);
    }

    public async Task<ReportStatisticsDocumentListResponseDto> GetDossierByDocumentTypeDocumentListAsync(
        DossierByDocumentTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByDocumentTypeFilter(filter, isAdmin, userUnitId);
        var documentTypeClause = BuildDocumentTypeChartDimensionClause(filter.DocumentTypeIds);

        var countSql = $@"
            SELECT COUNT(*)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            INNER JOIN DOCUMENT_TYPES dt ON doc.DOCUMENT_TYPE_ID = dt.ID AND NVL(dt.IsDeleted, 0) = 0
            WHERE {baseWhere}{documentTypeClause}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                doc.ID AS DocumentId,
                d.ID AS DossierId,
                dt.NAME AS DocumentTypeName,
                dt_dossier.NAME AS DossierTypeName,
                i.NAME AS InfrastructureName,
                (
                    SELECT LISTAGG(e.NAME, ', ') WITHIN GROUP (ORDER BY e.NAME)
                    FROM DOSSIER_EQUIPMENTS de
                    INNER JOIN Equipments e ON de.EquipmentId = e.Id AND NVL(e.IsDeleted, 0) = 0
                    WHERE de.DossierId = d.Id
                ) AS EquipmentName,
                doc.NAME AS DocumentName
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            INNER JOIN DOCUMENT_TYPES dt ON doc.DOCUMENT_TYPE_ID = dt.ID AND NVL(dt.IsDeleted, 0) = 0
            LEFT JOIN DOSSIER_TYPES dt_dossier ON d.DossierTypeId = dt_dossier.ID
            WHERE {baseWhere}{documentTypeClause}
            ORDER BY dt.NAME, i.NAME, doc.NAME
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDocumentListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            items.Add(new ReportStatisticsDocumentListItemDto
            {
                Stt = stt++,
                DocumentId = Convert.ToString(r.DOCUMENTID ?? r.DocumentId) ?? string.Empty,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId) ?? string.Empty,
                DocumentTypeName = Convert.ToString(r.DOCUMENTTYPENAME ?? r.DocumentTypeName) ?? "-",
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName) ?? "-",
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName) ?? "-",
                EquipmentName = Convert.ToString(r.EQUIPMENTNAME ?? r.EquipmentName) ?? "-",
                DocumentName = Convert.ToString(r.DOCUMENTNAME ?? r.DocumentName) ?? "-"
            });
        }

        return new ReportStatisticsDocumentListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsDocumentTypeGridResponseDto> GetDossierByDocumentTypeGridAsync(
        DossierByDocumentTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByDocumentTypeFilter(filter, isAdmin, userUnitId);
        var documentTypeClause = BuildDocumentTypeChartDimensionClause(filter.DocumentTypeIds);

        var countSql = $@"
            WITH filtered AS (
                SELECT DISTINCT dt.ID AS DocumentTypeId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
                INNER JOIN DOCUMENT_TYPES dt ON doc.DOCUMENT_TYPE_ID = dt.ID AND NVL(dt.IsDeleted, 0) = 0
                WHERE {baseWhere}{documentTypeClause}
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT doc.ID AS DocumentId, dt.ID AS DocumentTypeId, dt.CODE AS DocumentTypeCode,
                       dt.NAME AS DocumentTypeName
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
                INNER JOIN DOCUMENT_TYPES dt ON doc.DOCUMENT_TYPE_ID = dt.ID AND NVL(dt.IsDeleted, 0) = 0
                WHERE {baseWhere}{documentTypeClause}
            ),
            type_stats AS (
                SELECT
                    f.DocumentTypeId,
                    f.DocumentTypeCode,
                    f.DocumentTypeName,
                    COUNT(DISTINCT f.DocumentId) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = f.DocumentId AND dv.IS_DELETED = 0
                GROUP BY f.DocumentTypeId, f.DocumentTypeCode, f.DocumentTypeName
            )
            SELECT
                s.DocumentTypeCode,
                s.DocumentTypeName,
                s.TotalDocuments,
                s.TotalPages
            FROM type_stats s
            ORDER BY s.DocumentTypeName
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsDocumentTypeGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            items.Add(new ReportStatisticsDocumentTypeGridItemDto
            {
                Stt = stt++,
                DocumentTypeCode = Convert.ToString(r.DOCUMENTTYPECODE ?? r.DocumentTypeCode) ?? "-",
                DocumentTypeName = Convert.ToString(r.DOCUMENTTYPENAME ?? r.DocumentTypeName) ?? "-",
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0)
            });
        }

        return new ReportStatisticsDocumentTypeGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static (int Year, DynamicParameters Parameters, string BaseWhere) BuildDossierByDocumentTypeChartParameters(
        DossierByDocumentTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, _, _, parameters, baseWhere) =
            BuildDossierByDocumentTypeFilter(filter, isAdmin, userUnitId);
        return (targetYear, parameters, baseWhere);
    }

    private static (int Year, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByDocumentTypeFilter(
        DossierByDocumentTypeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears) = ResolveReportYearFilter(filter.Year);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendDocumentTypeFilterToParameters(filter.DocumentTypeIds, parameters);
        AppendReportYearFilterToWhere(allYears, targetYear, ref baseWhere, parameters);

        return (targetYear, page, pageSize, parameters, baseWhere);
    }

    private static List<string> NormalizeDocumentTypeIds(IEnumerable<string>? documentTypeIds)
    {
        if (documentTypeIds == null)
            return new List<string>();

        return documentTypeIds
            .SelectMany(id => (id ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendDocumentTypeFilterToParameters(
        IEnumerable<string>? documentTypeIds,
        DynamicParameters parameters)
    {
        var ids = NormalizeDocumentTypeIds(documentTypeIds);
        if (ids.Count == 0)
            return;

        parameters.Add("DocumentTypeIds", ids.ToArray());
    }

    private static string BuildDocumentTypeChartDimensionClause(IEnumerable<string>? documentTypeIds)
    {
        return NormalizeDocumentTypeIds(documentTypeIds).Count > 0
            ? " AND dt.ID IN :DocumentTypeIds"
            : string.Empty;
    }

    public async Task<DossierByStationSummaryStatsDto> GetDossierByStationSummaryStatsAsync(
        DossierByStationFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (targetYear, allYears, parameters, baseWhere) = BuildDossierByStationChartParameters(filter, isAdmin, userUnitId);
        var currentCalendarYear = DateTime.Now.Year;

        if (allYears || targetYear < currentCalendarYear)
        {
            var periodTotals = await QueryStationTotalsAsync(baseWhere, parameters);
            return new DossierByStationSummaryStatsDto
            {
                Year = allYears ? 0 : targetYear,
                ReferenceMonth = 0,
                PreviousMonth = 0,
                ShowGrowth = false,
                DossierCount = periodTotals.Dossiers,
                DocumentCount = periodTotals.Documents,
                PageCount = periodTotals.Pages
            };
        }

        var referenceMonth = DateTime.Now.Month;
        var previousMonth = referenceMonth > 1 ? referenceMonth - 1 : 0;

        var totals = await QueryStationTotalsAsync(baseWhere, parameters);
        var currentMonth = await QueryStationMonthCountsAsync(baseWhere, parameters, referenceMonth);
        var previousMonthCounts = previousMonth > 0
            ? await QueryStationMonthCountsAsync(baseWhere, parameters, previousMonth)
            : (Dossiers: 0L, Documents: 0L, Pages: 0L);

        var showGrowth = previousMonth > 0;

        return new DossierByStationSummaryStatsDto
        {
            Year = targetYear,
            ReferenceMonth = referenceMonth,
            PreviousMonth = previousMonth,
            ShowGrowth = showGrowth,
            DossierCount = totals.Dossiers,
            DossierGrowthPercent = showGrowth ? CalcGrowthPercent(currentMonth.Dossiers, previousMonthCounts.Dossiers) : null,
            DocumentCount = totals.Documents,
            DocumentGrowthPercent = showGrowth ? CalcGrowthPercent(currentMonth.Documents, previousMonthCounts.Documents) : null,
            PageCount = totals.Pages,
            PageGrowthPercent = showGrowth ? CalcGrowthPercent(currentMonth.Pages, previousMonthCounts.Pages) : null
        };
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByStationListAsync(
        DossierByStationFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, _, page, pageSize, parameters, baseWhere) = BuildDossierByStationFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsStationGridResponseDto> GetDossierByStationStationGridAsync(
        DossierByStationFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, _, page, pageSize, parameters, baseWhere) = BuildDossierByStationStationGridFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            WITH filtered AS (
                SELECT d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
                GROUP BY d.InfrastructureId
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
            ),
            infra_stats AS (
                SELECT
                    f.InfrastructureId,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.InfrastructureId
            )
            SELECT
                i.CODE AS InfrastructureCode,
                i.NAME AS InfrastructureName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM infra_stats s
            INNER JOIN INFRASTRUCTURE i ON i.ID = s.InfrastructureId
            ORDER BY i.NAME
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsStationGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var infraCode = Convert.ToString(r.INFRASTRUCTURECODE ?? r.InfrastructureCode);
            var infraName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName);
            items.Add(new ReportStatisticsStationGridItemDto
            {
                Stt = stt++,
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0),
                CatalogData = BuildInfrastructureCatalogData(infraCode, infraName, bhsColumns)
            });
        }

        return new ReportStatisticsStationGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task<(long Dossiers, long Documents, long Pages)> QueryStationMonthCountsAsync(
        string baseWhere,
        DynamicParameters parameters,
        int month)
    {
        var queryParams = new DynamicParameters(parameters);
        queryParams.Add("TargetMonth", month);

        var sql = $@"
            SELECT
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}
              AND EXTRACT(MONTH FROM d.CreatedDate) = :TargetMonth";

        var row = await _connection.QueryFirstOrDefaultAsync(sql, queryParams);
        if (row == null)
            return (0, 0, 0);

        return (
            Convert.ToInt64(row.DOSSIERCOUNT ?? row.DossierCount ?? 0),
            Convert.ToInt64(row.DOCUMENTCOUNT ?? row.DocumentCount ?? 0),
            Convert.ToInt64(row.PAGECOUNT ?? row.PageCount ?? 0)
        );
    }

    private async Task<(long Dossiers, long Documents, long Pages)> QueryStationTotalsAsync(
        string baseWhere,
        DynamicParameters parameters)
    {
        var sql = $@"
            SELECT
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}";

        var row = await _connection.QueryFirstOrDefaultAsync(sql, parameters);
        if (row == null)
            return (0, 0, 0);

        return (
            Convert.ToInt64(row.DOSSIERCOUNT ?? row.DossierCount ?? 0),
            Convert.ToInt64(row.DOCUMENTCOUNT ?? row.DocumentCount ?? 0),
            Convert.ToInt64(row.PAGECOUNT ?? row.PageCount ?? 0)
        );
    }

    private static (int TargetYear, bool AllYears, DynamicParameters Parameters, string BaseWhere) BuildDossierByStationChartParameters(
        DossierByStationFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears, _, _, parameters, baseWhere) =
            BuildDossierByStationFilter(filter, isAdmin, userUnitId);
        return (targetYear, allYears, parameters, baseWhere);
    }

    private static (int TargetYear, bool AllYears, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByStationStationGridFilter(
        DossierByStationFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears, page, pageSize, parameters, baseWhere) =
            BuildDossierByStationFilter(filter, isAdmin, userUnitId);

        return (targetYear, allYears, page, pageSize, parameters, baseWhere);
    }

    private static (int TargetYear, bool AllYears, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByStationFilter(
        DossierByStationFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var allYears = !filter.Year.HasValue || filter.Year.Value <= 0;
        var targetYear = allYears ? 0 : filter.Year!.Value;
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND i.INFRA_TYPE_ID = 1
            AND NVL(i.IsDeleted, 0) = 0";

        if (!allYears)
        {
            baseWhere += " AND EXTRACT(YEAR FROM d.CreatedDate) = :Year";
            parameters.Add("Year", targetYear);
        }

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendStationFilterToWhere(filter.StationIds, ref baseWhere, parameters);

        return (targetYear, allYears, page, pageSize, parameters, baseWhere);
    }

    private static decimal? CalcGrowthPercent(long current, long previous)
    {
        if (previous == 0)
            return current > 0 ? 100m : 0m;

        return Math.Round((decimal)(current - previous) / previous * 100m, 1);
    }

    private static (int TargetYear, bool AllYears) ResolveReportYearFilter(int? year)
    {
        var allYears = !year.HasValue || year.Value <= 0;
        return (allYears ? 0 : year!.Value, allYears);
    }

    private static void AppendReportYearFilterToWhere(
        bool allYears,
        int targetYear,
        ref string baseWhere,
        DynamicParameters parameters)
    {
        if (!allYears)
        {
            baseWhere += " AND EXTRACT(YEAR FROM d.CreatedDate) = :Year";
            parameters.Add("Year", targetYear);
        }
    }

    private static string GetReportYearSqlClause(bool allYears) =>
        allYears ? string.Empty : " AND EXTRACT(YEAR FROM d.CreatedDate) = :Year";

    private static List<string> NormalizeStationIds(IEnumerable<string>? stationIds)
    {
        if (stationIds == null)
            return new List<string>();

        return stationIds
            .SelectMany(id => (id ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendStationFilterToWhere(
        IEnumerable<string>? stationIds,
        ref string baseWhere,
        DynamicParameters parameters)
    {
        var ids = NormalizeStationIds(stationIds);
        if (ids.Count == 0)
            return;

        baseWhere += " AND d.InfrastructureId IN :StationIds";
        parameters.Add("StationIds", ids.ToArray());
    }

    public async Task<DossierByLineSummaryStatsDto> GetDossierByLineSummaryStatsAsync(
        DossierByLineFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (targetYear, allYears, parameters, baseWhere) = BuildDossierByLineChartParameters(filter, isAdmin, userUnitId);
        var currentCalendarYear = DateTime.Now.Year;

        if (allYears || targetYear < currentCalendarYear)
        {
            var periodTotals = await QueryLineTotalsAsync(baseWhere, parameters);
            return new DossierByLineSummaryStatsDto
            {
                Year = allYears ? 0 : targetYear,
                ReferenceMonth = 0,
                PreviousMonth = 0,
                ShowGrowth = false,
                DossierCount = periodTotals.Dossiers,
                DocumentCount = periodTotals.Documents,
                PageCount = periodTotals.Pages
            };
        }

        var referenceMonth = DateTime.Now.Month;
        var previousMonth = referenceMonth > 1 ? referenceMonth - 1 : 0;

        var totals = await QueryLineTotalsAsync(baseWhere, parameters);
        var currentMonth = await QueryLineMonthCountsAsync(baseWhere, parameters, referenceMonth);
        var previousMonthCounts = previousMonth > 0
            ? await QueryLineMonthCountsAsync(baseWhere, parameters, previousMonth)
            : (Dossiers: 0L, Documents: 0L, Pages: 0L);

        var showGrowth = previousMonth > 0;

        return new DossierByLineSummaryStatsDto
        {
            Year = targetYear,
            ReferenceMonth = referenceMonth,
            PreviousMonth = previousMonth,
            ShowGrowth = showGrowth,
            DossierCount = totals.Dossiers,
            DossierGrowthPercent = showGrowth ? CalcGrowthPercent(currentMonth.Dossiers, previousMonthCounts.Dossiers) : null,
            DocumentCount = totals.Documents,
            DocumentGrowthPercent = showGrowth ? CalcGrowthPercent(currentMonth.Documents, previousMonthCounts.Documents) : null,
            PageCount = totals.Pages,
            PageGrowthPercent = showGrowth ? CalcGrowthPercent(currentMonth.Pages, previousMonthCounts.Pages) : null
        };
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByLineListAsync(
        DossierByLineFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, _, page, pageSize, parameters, baseWhere) = BuildDossierByLineFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsStationGridResponseDto> GetDossierByLineLineGridAsync(
        DossierByLineFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, _, page, pageSize, parameters, baseWhere) = BuildDossierByLineLineGridFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            WITH filtered AS (
                SELECT d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
                GROUP BY d.InfrastructureId
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
            ),
            infra_stats AS (
                SELECT
                    f.InfrastructureId,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.InfrastructureId
            )
            SELECT
                i.CODE AS InfrastructureCode,
                i.NAME AS InfrastructureName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM infra_stats s
            INNER JOIN INFRASTRUCTURE i ON i.ID = s.InfrastructureId
            ORDER BY i.NAME
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsStationGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var infraCode = Convert.ToString(r.INFRASTRUCTURECODE ?? r.InfrastructureCode);
            var infraName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName);
            items.Add(new ReportStatisticsStationGridItemDto
            {
                Stt = stt++,
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0),
                CatalogData = BuildInfrastructureCatalogData(infraCode, infraName, bhsColumns)
            });
        }

        return new ReportStatisticsStationGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task<(long Dossiers, long Documents, long Pages)> QueryLineMonthCountsAsync(
        string baseWhere,
        DynamicParameters parameters,
        int month)
    {
        var queryParams = new DynamicParameters(parameters);
        queryParams.Add("TargetMonth", month);

        var sql = $@"
            SELECT
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}
              AND EXTRACT(MONTH FROM d.CreatedDate) = :TargetMonth";

        var row = await _connection.QueryFirstOrDefaultAsync(sql, queryParams);
        if (row == null)
            return (0, 0, 0);

        return (
            Convert.ToInt64(row.DOSSIERCOUNT ?? row.DossierCount ?? 0),
            Convert.ToInt64(row.DOCUMENTCOUNT ?? row.DocumentCount ?? 0),
            Convert.ToInt64(row.PAGECOUNT ?? row.PageCount ?? 0)
        );
    }

    private async Task<(long Dossiers, long Documents, long Pages)> QueryLineTotalsAsync(
        string baseWhere,
        DynamicParameters parameters)
    {
        var sql = $@"
            SELECT
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}";

        var row = await _connection.QueryFirstOrDefaultAsync(sql, parameters);
        if (row == null)
            return (0, 0, 0);

        return (
            Convert.ToInt64(row.DOSSIERCOUNT ?? row.DossierCount ?? 0),
            Convert.ToInt64(row.DOCUMENTCOUNT ?? row.DocumentCount ?? 0),
            Convert.ToInt64(row.PAGECOUNT ?? row.PageCount ?? 0)
        );
    }

    private static (int TargetYear, bool AllYears, DynamicParameters Parameters, string BaseWhere) BuildDossierByLineChartParameters(
        DossierByLineFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears, _, _, parameters, baseWhere) =
            BuildDossierByLineFilter(filter, isAdmin, userUnitId);
        return (targetYear, allYears, parameters, baseWhere);
    }

    private static (int TargetYear, bool AllYears, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByLineLineGridFilter(
        DossierByLineFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (targetYear, allYears, page, pageSize, parameters, baseWhere) =
            BuildDossierByLineFilter(filter, isAdmin, userUnitId);

        return (targetYear, allYears, page, pageSize, parameters, baseWhere);
    }

    private static (int TargetYear, bool AllYears, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByLineFilter(
        DossierByLineFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var allYears = !filter.Year.HasValue || filter.Year.Value <= 0;
        var targetYear = allYears ? 0 : filter.Year!.Value;
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND i.INFRA_TYPE_ID = 2
            AND NVL(i.IsDeleted, 0) = 0";

        if (!allYears)
        {
            baseWhere += " AND EXTRACT(YEAR FROM d.CreatedDate) = :Year";
            parameters.Add("Year", targetYear);
        }

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendLineFilterToWhere(filter.LineIds, ref baseWhere, parameters);

        return (targetYear, allYears, page, pageSize, parameters, baseWhere);
    }

    private static List<string> NormalizeLineIds(IEnumerable<string>? lineIds)
    {
        if (lineIds == null)
            return new List<string>();

        return lineIds
            .SelectMany(id => (id ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendLineFilterToWhere(
        IEnumerable<string>? lineIds,
        ref string baseWhere,
        DynamicParameters parameters)
    {
        var ids = NormalizeLineIds(lineIds);
        if (ids.Count == 0)
            return;

        baseWhere += " AND d.InfrastructureId IN :LineIds";
        parameters.Add("LineIds", ids.ToArray());
    }

    public async Task<IEnumerable<int>> GetAvailableOperationYearsAsync()
    {
        EnsureOpen();
        const string sql = @"
            SELECT DISTINCT EXTRACT(YEAR FROM i.OPERATION_DATE) AS Yr
            FROM INFRASTRUCTURE i
            INNER JOIN DOSSIERS d ON d.InfrastructureId = i.ID
            WHERE i.INFRA_TYPE_ID IN (1, 2)
              AND NVL(i.IsDeleted, 0) = 0
              AND i.OPERATION_DATE IS NOT NULL
              AND d.IsDeleted = 0 AND d.STATUS_ID = 6 AND d.PUBLISHSTATUSID = 2
            ORDER BY Yr DESC";

        var years = (await _connection.QueryAsync<int?>(sql))
            .Where(y => y.HasValue && y.Value > 1900)
            .Select(y => y!.Value)
            .ToList();

        return years;
    }

    public async Task<DossierByOperationYearSummaryStatsDto> GetDossierByOperationYearSummaryStatsAsync(
        DossierByOperationYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (targetYear, allYears, _, _, parameters, baseWhere) = BuildDossierByOperationYearFilter(filter, isAdmin, userUnitId);
        var totals = await QueryStationTotalsAsync(baseWhere, parameters);

        if (allYears)
        {
            return new DossierByOperationYearSummaryStatsDto
            {
                Year = 0,
                PreviousYear = 0,
                ShowGrowth = false,
                DossierCount = totals.Dossiers,
                DocumentCount = totals.Documents,
                PageCount = totals.Pages
            };
        }

        var previousYear = targetYear - 1;
        var (_, _, _, _, prevParameters, prevBaseWhere) = BuildDossierByOperationYearFilter(filter, isAdmin, userUnitId, previousYear);
        var previousTotals = await QueryStationTotalsAsync(prevBaseWhere, prevParameters);

        return new DossierByOperationYearSummaryStatsDto
        {
            Year = targetYear,
            PreviousYear = previousYear,
            ShowGrowth = true,
            DossierCount = totals.Dossiers,
            DossierGrowthPercent = CalcGrowthPercent(totals.Dossiers, previousTotals.Dossiers),
            DocumentCount = totals.Documents,
            DocumentGrowthPercent = CalcGrowthPercent(totals.Documents, previousTotals.Documents),
            PageCount = totals.Pages,
            PageGrowthPercent = CalcGrowthPercent(totals.Pages, previousTotals.Pages)
        };
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByOperationYearListAsync(
        DossierByOperationYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, _, page, pageSize, parameters, baseWhere) = BuildDossierByOperationYearFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsStationGridResponseDto> GetDossierByOperationYearStationGridAsync(
        DossierByOperationYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, _, page, pageSize, parameters, baseWhere) = BuildDossierByOperationYearFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            WITH filtered AS (
                SELECT d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
                GROUP BY d.InfrastructureId
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
            ),
            infra_stats AS (
                SELECT
                    f.InfrastructureId,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.InfrastructureId
            )
            SELECT
                i.CODE AS InfrastructureCode,
                i.NAME AS InfrastructureName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM infra_stats s
            INNER JOIN INFRASTRUCTURE i ON i.ID = s.InfrastructureId
            ORDER BY i.NAME
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsStationGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var infraCode = Convert.ToString(r.INFRASTRUCTURECODE ?? r.InfrastructureCode);
            var infraName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName);
            items.Add(new ReportStatisticsStationGridItemDto
            {
                Stt = stt++,
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0),
                CatalogData = BuildInfrastructureCatalogData(infraCode, infraName, bhsColumns)
            });
        }

        return new ReportStatisticsStationGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Filter báo cáo theo năm vận hành: năm so sánh với INFRASTRUCTURE.OPERATION_DATE (không phải d.CreatedDate).
    /// Đối tượng Trạm/Đường dây gộp cả hồ sơ thiết bị của trạm/đường dây tương ứng (DOSSIER_GROUP_ID 1+3 / 2+4).
    /// </summary>
    private static (int TargetYear, bool AllYears, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByOperationYearFilter(
        DossierByOperationYearFilterDto filter,
        bool isAdmin,
        long? userUnitId,
        int? yearOverride = null)
    {
        var year = yearOverride ?? filter.Year;
        var allYears = !year.HasValue || year.Value <= 0;
        var targetYear = allYears ? 0 : year!.Value;
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND i.INFRA_TYPE_ID IN (1, 2)
            AND NVL(i.IsDeleted, 0) = 0";

        if (!allYears)
        {
            baseWhere += " AND EXTRACT(YEAR FROM i.OPERATION_DATE) = :Year";
            parameters.Add("Year", targetYear);
        }

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendOperationYearObjectTypeFilterToWhere(filter.ObjectType, ref baseWhere);

        return (targetYear, allYears, page, pageSize, parameters, baseWhere);
    }

    private static void AppendOperationYearObjectTypeFilterToWhere(int? objectType, ref string baseWhere)
    {
        if (!objectType.HasValue || objectType.Value <= 0)
            return;

        if (objectType.Value == 1)
            baseWhere += " AND NVL(d.DOSSIER_GROUP_ID, 1) IN (1, 3)";
        else if (objectType.Value == 2)
            baseWhere += " AND NVL(d.DOSSIER_GROUP_ID, 1) IN (2, 4)";
    }

    public async Task<DossierByOperationTimeSummaryStatsDto> GetDossierByOperationTimeSummaryStatsAsync(
        DossierByOperationTimeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, _, parameters, baseWhere) = BuildDossierByOperationTimeFilter(filter, isAdmin, userUnitId);
        var totals = await QueryStationTotalsAsync(baseWhere, parameters);

        return new DossierByOperationTimeSummaryStatsDto
        {
            DossierCount = totals.Dossiers,
            DocumentCount = totals.Documents,
            PageCount = totals.Pages
        };
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByOperationTimeListAsync(
        DossierByOperationTimeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (page, pageSize, parameters, baseWhere) = BuildDossierByOperationTimeFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsStationGridResponseDto> GetDossierByOperationTimeStationGridAsync(
        DossierByOperationTimeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (page, pageSize, parameters, baseWhere) = BuildDossierByOperationTimeFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            WITH filtered AS (
                SELECT d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
                GROUP BY d.InfrastructureId
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
            ),
            infra_stats AS (
                SELECT
                    f.InfrastructureId,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.InfrastructureId
            )
            SELECT
                i.CODE AS InfrastructureCode,
                i.NAME AS InfrastructureName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM infra_stats s
            INNER JOIN INFRASTRUCTURE i ON i.ID = s.InfrastructureId
            ORDER BY i.NAME
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsStationGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var infraCode = Convert.ToString(r.INFRASTRUCTURECODE ?? r.InfrastructureCode);
            var infraName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName);
            items.Add(new ReportStatisticsStationGridItemDto
            {
                Stt = stt++,
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0),
                CatalogData = BuildInfrastructureCatalogData(infraCode, infraName, bhsColumns)
            });
        }

        return new ReportStatisticsStationGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Filter báo cáo theo thời gian vận hành: khoảng ngày so sánh với INFRASTRUCTURE.OPERATION_DATE
    /// (không phải d.CreatedDate). Đối tượng Trạm/Đường dây gộp cả hồ sơ thiết bị tương ứng (DOSSIER_GROUP_ID 1+3 / 2+4).
    /// </summary>
    private static (int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByOperationTimeFilter(
        DossierByOperationTimeFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();
        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND i.INFRA_TYPE_ID IN (1, 2)
            AND NVL(i.IsDeleted, 0) = 0";

        if (filter.FromDate.HasValue)
        {
            baseWhere += " AND i.OPERATION_DATE >= :FromDate";
            parameters.Add("FromDate", filter.FromDate.Value.Date);
        }

        if (filter.ToDate.HasValue)
        {
            baseWhere += " AND i.OPERATION_DATE < :ToDate";
            parameters.Add("ToDate", filter.ToDate.Value.Date.AddDays(1));
        }

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendOperationYearObjectTypeFilterToWhere(filter.ObjectType, ref baseWhere);

        return (page, pageSize, parameters, baseWhere);
    }

    public async Task<IEnumerable<int>> GetAvailableManufactureYearsAsync()
    {
        EnsureOpen();
        const string sql = @"
            SELECT DISTINCT e.MANUFACTURE_YEAR AS Yr
            FROM Equipments e
            INNER JOIN DOSSIER_EQUIPMENTS de ON de.EquipmentId = e.Id
            INNER JOIN DOSSIERS d ON d.Id = de.DossierId
            WHERE e.MANUFACTURE_YEAR IS NOT NULL
              AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
              AND d.IsDeleted = 0 AND d.STATUS_ID = 6 AND d.PUBLISHSTATUSID = 2
            ORDER BY Yr DESC";

        var years = (await _connection.QueryAsync<int?>(sql))
            .Where(y => y.HasValue && y.Value > 1900)
            .Select(y => y!.Value)
            .ToList();

        return years;
    }

    public async Task<IEnumerable<DossierByManufactureYearChartStatDto>> GetDossierByManufactureYearChartStatsAsync(
        DossierByManufactureYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (allYears, parameters, baseWhere) = BuildDossierByManufactureYearFilter(filter, isAdmin, userUnitId);
        var manufactureYearClause = BuildManufactureYearDimensionClause(allYears);

        var sql = $@"
            SELECT
                et.CODE AS EquipmentTypeCode,
                et.NAME AS EquipmentTypeName,
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
            INNER JOIN Equipments e ON de.EquipmentId = e.Id AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
            INNER JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}{manufactureYearClause}
            GROUP BY et.ID, et.CODE, et.NAME
            ORDER BY et.NAME";

        return await _connection.QueryAsync<DossierByManufactureYearChartStatDto>(sql, parameters);
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByManufactureYearListAsync(
        DossierByManufactureYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (_, page, pageSize, parameters, baseWhere) = BuildDossierByManufactureYearListFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsEquipmentGridResponseDto> GetDossierByManufactureYearEquipmentGridAsync(
        DossierByManufactureYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (allYears, page, pageSize, parameters, baseWhere) = BuildDossierByManufactureYearListFilter(filter, isAdmin, userUnitId);
        var manufactureYearClause = BuildManufactureYearDimensionClause(allYears);

        var countSql = $@"
            WITH filtered AS (
                SELECT DISTINCT e.Id AS EquipmentId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
                INNER JOIN Equipments e ON de.EquipmentId = e.Id AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
                WHERE {baseWhere}{manufactureYearClause}
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, e.Id AS EquipmentId, e.Code AS EquipmentCode, e.Name AS EquipmentName,
                       i.NAME AS InfrastructureName, e.MANUFACTURE_YEAR AS ManufactureYear
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
                INNER JOIN Equipments e ON de.EquipmentId = e.Id AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
                WHERE {baseWhere}{manufactureYearClause}
            ),
            equip_stats AS (
                SELECT
                    f.EquipmentId, f.EquipmentCode, f.EquipmentName, f.InfrastructureName, f.ManufactureYear,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.EquipmentId, f.EquipmentCode, f.EquipmentName, f.InfrastructureName, f.ManufactureYear
            )
            SELECT
                EquipmentCode, EquipmentName, InfrastructureName, ManufactureYear,
                TotalDossiers, TotalDocuments, TotalPages
            FROM equip_stats
            ORDER BY EquipmentName
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsEquipmentGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            object manufactureYearObj = r.MANUFACTUREYEAR ?? r.ManufactureYear;
            items.Add(new ReportStatisticsEquipmentGridItemDto
            {
                Stt = stt++,
                EquipmentCode = Convert.ToString(r.EQUIPMENTCODE ?? r.EquipmentCode) ?? "-",
                EquipmentName = Convert.ToString(r.EQUIPMENTNAME ?? r.EquipmentName) ?? "-",
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName) ?? "-",
                ManufactureYear = manufactureYearObj == null || manufactureYearObj is DBNull
                    ? null
                    : Convert.ToInt32(manufactureYearObj),
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0)
            });
        }

        return new ReportStatisticsEquipmentGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Filter chung: hồ sơ thiết bị của trạm/đường dây (DOSSIER_EQUIPMENTS), lọc theo đơn vị,
    /// trạm/đường dây cụ thể (StationIds) và năm sản xuất thiết bị (EQUIPMENTS.MANUFACTURE_YEAR).
    /// Dùng EXISTS cho list/count (không cần enumerate từng thiết bị); chart/equipment-grid enumerate
    /// trực tiếp qua JOIN + <see cref="BuildManufactureYearDimensionClause"/> (cùng tham số :ManufactureYear).
    /// </summary>
    private static (bool AllYears, DynamicParameters Parameters, string BaseWhere) BuildDossierByManufactureYearFilter(
        DossierByManufactureYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var allYears = !filter.ManufactureYear.HasValue || filter.ManufactureYear.Value <= 0;
        var targetYear = allYears ? 0 : filter.ManufactureYear!.Value;

        var parameters = new DynamicParameters();
        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND i.INFRA_TYPE_ID IN (1, 2)
            AND NVL(i.IsDeleted, 0) = 0";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendStationFilterToWhere(filter.StationIds, ref baseWhere, parameters);

        baseWhere += @" AND EXISTS (
            SELECT 1 FROM DOSSIER_EQUIPMENTS de_f
            INNER JOIN Equipments e_f ON de_f.EquipmentId = e_f.Id
            WHERE de_f.DossierId = d.Id
              AND (e_f.IsDeleted = 0 OR e_f.IsDeleted IS NULL)";

        if (!allYears)
        {
            baseWhere += " AND e_f.MANUFACTURE_YEAR = :ManufactureYear";
            parameters.Add("ManufactureYear", targetYear);
        }

        baseWhere += ")";

        return (allYears, parameters, baseWhere);
    }

    private static (bool AllYears, int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByManufactureYearListFilter(
        DossierByManufactureYearFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (allYears, parameters, baseWhere) = BuildDossierByManufactureYearFilter(filter, isAdmin, userUnitId);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        return (allYears, page, pageSize, parameters, baseWhere);
    }

    private static string BuildManufactureYearDimensionClause(bool allYears) =>
        allYears ? string.Empty : " AND e.MANUFACTURE_YEAR = :ManufactureYear";

    public async Task<IEnumerable<ReportDossierLookupItem>> GetEquipmentStatusesAsync()
    {
        EnsureOpen();
        const string sql = @"
            SELECT CAST(c.Id AS VARCHAR2(50)) AS Id, c.Name AS Name, c.Code AS Code
            FROM CATALOG c
            INNER JOIN CATALOG_TYPE ct ON c.CatalogTypeId = ct.Id
            WHERE ct.Code = 'EQUIPMENT_STATUS'
              AND c.IsDeleted = 0
              AND ct.IsDeleted = 0
            ORDER BY c.Priority ASC, c.Name ASC";

        return await _connection.QueryAsync<ReportDossierLookupItem>(sql);
    }

    public async Task<IEnumerable<DossierByEquipmentStatusChartStatDto>> GetDossierByEquipmentStatusChartStatsAsync(
        DossierByEquipmentStatusFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (parameters, baseWhere) = BuildDossierByEquipmentStatusFilter(filter, isAdmin, userUnitId);
        var statusClause = BuildEquipmentStatusDimensionClause(filter.EquipmentStatusIds);

        var sql = $@"
            SELECT
                et.CODE AS EquipmentTypeCode,
                et.NAME AS EquipmentTypeName,
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
            INNER JOIN Equipments e ON de.EquipmentId = e.Id AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
            INNER JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}{statusClause}
            GROUP BY et.ID, et.CODE, et.NAME
            ORDER BY et.NAME";

        return await _connection.QueryAsync<DossierByEquipmentStatusChartStatDto>(sql, parameters);
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierByEquipmentStatusListAsync(
        DossierByEquipmentStatusFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (page, pageSize, parameters, baseWhere) = BuildDossierByEquipmentStatusListFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsEquipmentStatusGridResponseDto> GetDossierByEquipmentStatusEquipmentGridAsync(
        DossierByEquipmentStatusFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (page, pageSize, parameters, baseWhere) = BuildDossierByEquipmentStatusListFilter(filter, isAdmin, userUnitId);
        var statusClause = BuildEquipmentStatusDimensionClause(filter.EquipmentStatusIds);

        var countSql = $@"
            WITH filtered AS (
                SELECT DISTINCT e.Id AS EquipmentId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
                INNER JOIN Equipments e ON de.EquipmentId = e.Id AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
                WHERE {baseWhere}{statusClause}
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, e.Id AS EquipmentId, e.Code AS EquipmentCode, e.Name AS EquipmentName,
                       i.NAME AS InfrastructureName, cat.NAME AS EquipmentStatusName
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
                INNER JOIN Equipments e ON de.EquipmentId = e.Id AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
                LEFT JOIN CATALOG cat ON cat.Id = e.EQUIPMENT_STATUS_ID
                WHERE {baseWhere}{statusClause}
            ),
            equip_stats AS (
                SELECT
                    f.EquipmentId, f.EquipmentCode, f.EquipmentName, f.InfrastructureName, f.EquipmentStatusName,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.EquipmentId, f.EquipmentCode, f.EquipmentName, f.InfrastructureName, f.EquipmentStatusName
            )
            SELECT
                EquipmentCode, EquipmentName, InfrastructureName, EquipmentStatusName,
                TotalDossiers, TotalDocuments, TotalPages
            FROM equip_stats
            ORDER BY EquipmentName
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsEquipmentStatusGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            items.Add(new ReportStatisticsEquipmentStatusGridItemDto
            {
                Stt = stt++,
                EquipmentCode = Convert.ToString(r.EQUIPMENTCODE ?? r.EquipmentCode) ?? "-",
                EquipmentName = Convert.ToString(r.EQUIPMENTNAME ?? r.EquipmentName) ?? "-",
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName) ?? "-",
                EquipmentStatusName = Convert.ToString(r.EQUIPMENTSTATUSNAME ?? r.EquipmentStatusName) ?? "-",
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0)
            });
        }

        return new ReportStatisticsEquipmentStatusGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Filter chung: hồ sơ thiết bị (DOSSIER_GROUP_ID 3/4) của trạm/đường dây, lọc theo đơn vị,
    /// trạm/đường dây cụ thể (StationIds) và tình trạng thiết bị (EQUIPMENTS.EQUIPMENT_STATUS_ID).
    /// </summary>
    private static (DynamicParameters Parameters, string BaseWhere) BuildDossierByEquipmentStatusFilter(
        DossierByEquipmentStatusFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var parameters = new DynamicParameters();
        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND i.INFRA_TYPE_ID IN (1, 2)
            AND NVL(i.IsDeleted, 0) = 0
            AND NVL(d.DOSSIER_GROUP_ID, 1) IN (3, 4)";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendStationFilterToWhere(filter.StationIds, ref baseWhere, parameters);

        var statusIds = NormalizeStationIds(filter.EquipmentStatusIds);

        baseWhere += @" AND EXISTS (
            SELECT 1 FROM DOSSIER_EQUIPMENTS de_f
            INNER JOIN Equipments e_f ON de_f.EquipmentId = e_f.Id
            WHERE de_f.DossierId = d.Id
              AND (e_f.IsDeleted = 0 OR e_f.IsDeleted IS NULL)";

        if (statusIds.Count > 0)
        {
            baseWhere += " AND e_f.EQUIPMENT_STATUS_ID IN :EquipmentStatusIds";
            parameters.Add("EquipmentStatusIds", statusIds.ToArray());
        }

        baseWhere += ")";

        return (parameters, baseWhere);
    }

    private static (int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierByEquipmentStatusListFilter(
        DossierByEquipmentStatusFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (parameters, baseWhere) = BuildDossierByEquipmentStatusFilter(filter, isAdmin, userUnitId);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        return (page, pageSize, parameters, baseWhere);
    }

    private static string BuildEquipmentStatusDimensionClause(IEnumerable<string>? equipmentStatusIds)
    {
        return NormalizeStationIds(equipmentStatusIds).Count > 0
            ? " AND e.EQUIPMENT_STATUS_ID IN :EquipmentStatusIds"
            : string.Empty;
    }

    public async Task<IEnumerable<DossierGeneralInputChartStatDto>> GetDossierGeneralInputChartStatsAsync(
        DossierGeneralInputFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (parameters, baseWhere) = BuildDossierGeneralInputFilter(filter, isAdmin, userUnitId);

        var sql = $@"
            SELECT
                CASE
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN 'STATION'
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN 'LINE'
                    ELSE 'EQUIPMENT'
                END AS GroupCode,
                CASE
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN N'Trạm biến áp'
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN N'Đường dây'
                    ELSE N'Thiết bị'
                END AS GroupName,
                COUNT(DISTINCT d.ID) AS DossierCount,
                COUNT(DISTINCT doc.ID) AS DocumentCount,
                NVL(SUM(dv.PAGE_COUNT), 0) AS PageCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY
                CASE
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN 'STATION'
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN 'LINE'
                    ELSE 'EQUIPMENT'
                END,
                CASE
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 1 THEN N'Trạm biến áp'
                    WHEN NVL(d.DOSSIER_GROUP_ID, 1) = 2 THEN N'Đường dây'
                    ELSE N'Thiết bị'
                END";

        var rows = (await _connection.QueryAsync<DossierGeneralInputChartStatDto>(sql, parameters)).ToList();

        var defaultGroups = new List<(string Code, string Name)>
        {
            ("STATION", "Trạm biến áp"),
            ("LINE", "Đường dây"),
            ("EQUIPMENT", "Thiết bị")
        };

        if (filter.ObjectType.HasValue && filter.ObjectType.Value > 0)
        {
            if (filter.ObjectType.Value == 1) defaultGroups = defaultGroups.Where(g => g.Code == "STATION").ToList();
            else if (filter.ObjectType.Value == 2) defaultGroups = defaultGroups.Where(g => g.Code == "LINE").ToList();
            else if (filter.ObjectType.Value == 3) defaultGroups = defaultGroups.Where(g => g.Code == "EQUIPMENT").ToList();
        }

        var result = new List<DossierGeneralInputChartStatDto>();
        foreach (var (code, name) in defaultGroups)
        {
            var match = rows.FirstOrDefault(r => string.Equals(r.GroupCode, code, StringComparison.OrdinalIgnoreCase));
            result.Add(match ?? new DossierGeneralInputChartStatDto
            {
                GroupCode = code,
                GroupName = name,
                DossierCount = 0,
                DocumentCount = 0,
                PageCount = 0
            });
        }

        return result;
    }

    public async Task<IEnumerable<DossierGeneralInputRatioStatDto>> GetDossierGeneralInputRatioStatsAsync(
        DossierGeneralInputFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var chartStats = await GetDossierGeneralInputChartStatsAsync(filter, isAdmin, userUnitId);
        var totalDossiers = chartStats.Sum(s => s.DossierCount);

        return chartStats.Select(s => new DossierGeneralInputRatioStatDto
        {
            GroupCode = s.GroupCode,
            GroupName = s.GroupName,
            DossierCount = s.DossierCount,
            Percentage = totalDossiers > 0 ? Math.Round((decimal)s.DossierCount / totalDossiers * 100, 2) : 0
        });
    }

    public async Task<ReportStatisticsDossierListResponseDto> GetDossierGeneralInputListAsync(
        DossierGeneralInputFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (page, pageSize, parameters, baseWhere) = BuildDossierGeneralInputListFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            SELECT COUNT(DISTINCT d.ID)
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            WHERE {baseWhere}";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var listSql = $@"
            SELECT
                d.ID AS DossierId,
                i.NAME AS InfrastructureName,
                dt.NAME AS DossierTypeName,
                MAX(DBMS_LOB.SUBSTR(d.FORMDATAJSON, 4000, 1)) AS FormDataJson,
                COUNT(DISTINCT doc.ID) AS DocumentCount
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0
            WHERE {baseWhere}
            GROUP BY d.ID, i.NAME, dt.NAME, d.CreatedDate
            ORDER BY d.CreatedDate DESC
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(listSql, parameters)).ToList();
        var items = new List<ReportStatisticsDossierListItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var formDataJson = Convert.ToString(r.FORMDATAJSON ?? r.FormDataJson);
            items.Add(new ReportStatisticsDossierListItemDto
            {
                Stt = stt++,
                DossierId = Convert.ToString(r.DOSSIERID ?? r.DossierId),
                InfrastructureName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName),
                DossierTypeName = Convert.ToString(r.DOSSIERTYPENAME ?? r.DossierTypeName),
                DocumentCount = Convert.ToInt64(r.DOCUMENTCOUNT ?? r.DocumentCount ?? 0),
                CatalogData = ParseBhsCatalogData(formDataJson, bhsColumns)
            });
        }

        return new ReportStatisticsDossierListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportStatisticsStationGridResponseDto> GetDossierGeneralInputStationGridAsync(
        DossierGeneralInputFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var (page, pageSize, parameters, baseWhere) = BuildDossierGeneralInputStationGridFilter(filter, isAdmin, userUnitId);

        var countSql = $@"
            WITH filtered AS (
                SELECT d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
                GROUP BY d.InfrastructureId
            )
            SELECT COUNT(*) FROM filtered";

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var gridSql = $@"
            WITH filtered AS (
                SELECT d.ID AS DossierId, d.InfrastructureId
                FROM DOSSIERS d
                INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                WHERE {baseWhere}
            ),
            infra_stats AS (
                SELECT
                    f.InfrastructureId,
                    COUNT(DISTINCT f.DossierId) AS TotalDossiers,
                    COUNT(DISTINCT doc.ID) AS TotalDocuments,
                    NVL(SUM(dv.PAGE_COUNT), 0) AS TotalPages
                FROM filtered f
                LEFT JOIN DOCUMENTS doc ON doc.DOSSIER_ID = f.DossierId AND doc.IS_DELETED = 0
                LEFT JOIN DOCUMENT_VERSIONS dv ON dv.DOCUMENT_ID = doc.ID AND dv.IS_DELETED = 0
                GROUP BY f.InfrastructureId
            )
            SELECT
                i.CODE AS InfrastructureCode,
                i.NAME AS InfrastructureName,
                gt.NAME AS GridTypeName,
                s.TotalDossiers,
                s.TotalDocuments,
                s.TotalPages
            FROM infra_stats s
            INNER JOIN INFRASTRUCTURE i ON i.ID = s.InfrastructureId
            LEFT JOIN GridTypes gt ON i.GridTypeId = gt.Id
            ORDER BY i.NAME
            OFFSET :Offset ROWS
            FETCH NEXT :PageSize ROWS ONLY";

        var bhsColumns = (await GetBhsColumnsAsync()).ToList();
        var rawRows = (await _connection.QueryAsync(gridSql, parameters)).ToList();
        var items = new List<ReportStatisticsStationGridItemDto>();

        int stt = offset + 1;
        foreach (var r in rawRows)
        {
            var infraCode = Convert.ToString(r.INFRASTRUCTURECODE ?? r.InfrastructureCode);
            var infraName = Convert.ToString(r.INFRASTRUCTURENAME ?? r.InfrastructureName);
            items.Add(new ReportStatisticsStationGridItemDto
            {
                Stt = stt++,
                GridTypeName = Convert.ToString(r.GRIDTYPENAME ?? r.GridTypeName),
                TotalDossiers = Convert.ToInt64(r.TOTALDOSSIERS ?? r.TotalDossiers ?? 0),
                TotalDocuments = Convert.ToInt64(r.TOTALDOCUMENTS ?? r.TotalDocuments ?? 0),
                TotalPages = Convert.ToInt64(r.TOTALPAGES ?? r.TotalPages ?? 0),
                CatalogData = BuildInfrastructureCatalogData(infraCode, infraName, bhsColumns)
            });
        }

        return new ReportStatisticsStationGridResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static (int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierGeneralInputStationGridFilter(
        DossierGeneralInputFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (page, pageSize, parameters, baseWhere) = BuildDossierGeneralInputListFilter(filter, isAdmin, userUnitId);
        baseWhere += " AND NVL(i.IsDeleted, 0) = 0";

        return (page, pageSize, parameters, baseWhere);
    }

    private static (int Page, int PageSize, DynamicParameters Parameters, string BaseWhere) BuildDossierGeneralInputListFilter(
        DossierGeneralInputFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var (parameters, baseWhere) = BuildDossierGeneralInputFilter(filter, isAdmin, userUnitId);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        return (page, pageSize, parameters, baseWhere);
    }

    /// <summary>
    /// Báo cáo thống kê tổng hợp hồ sơ nhập liệu — giống hệt BuildDossierByYearFilter nhưng lọc theo
    /// khoảng ngày (d.CreatedDate) thay vì năm đơn.
    /// </summary>
    private static (DynamicParameters Parameters, string BaseWhere) BuildDossierGeneralInputFilter(
        DossierGeneralInputFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        var parameters = new DynamicParameters();
        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2";

        if (filter.FromDate.HasValue)
        {
            baseWhere += " AND d.CreatedDate >= :FromDate";
            parameters.Add("FromDate", filter.FromDate.Value.Date);
        }

        if (filter.ToDate.HasValue)
        {
            baseWhere += " AND d.CreatedDate < :ToDate";
            parameters.Add("ToDate", filter.ToDate.Value.Date.AddDays(1));
        }

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendObjectTypeFilterToWhere(filter.ObjectType, ref baseWhere);

        return (parameters, baseWhere);
    }
}
