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
        var targetYear = filter.Year.HasValue && filter.Year.Value > 0 ? filter.Year.Value : DateTime.Now.Year;
        var parameters = new DynamicParameters();
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
              AND d.PUBLISHSTATUSID = 2
              AND EXTRACT(YEAR FROM d.CreatedDate) = :Year";

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

    public async Task<IEnumerable<DossierByVoltageGridChartStatDto>> GetDossierByVoltageGridChartStatsAsync(
        DossierByVoltageGridFilterDto filter,
        bool isAdmin,
        long? userUnitId)
    {
        EnsureOpen();
        var targetYear = filter.Year.HasValue && filter.Year.Value > 0 ? filter.Year.Value : DateTime.Now.Year;
        var parameters = new DynamicParameters();
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
              AND d.PUBLISHSTATUSID = 2
              AND EXTRACT(YEAR FROM d.CreatedDate) = :Year";

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
        var targetYear = filter.Year.HasValue && filter.Year.Value > 0 ? filter.Year.Value : DateTime.Now.Year;
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();
        parameters.Add("Year", targetYear);

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND EXTRACT(YEAR FROM d.CreatedDate) = :Year";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendObjectTypeFilterToWhere(filter.ObjectType, ref baseWhere);
        AppendGridTypeFilterToWhere(filter.GridTypeId, ref baseWhere, parameters);

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
        var targetYear = filter.Year.HasValue && filter.Year.Value > 0 ? filter.Year.Value : DateTime.Now.Year;
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();
        parameters.Add("Year", targetYear);

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND EXTRACT(YEAR FROM d.CreatedDate) = :Year";

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
        var targetYear = filter.Year.HasValue && filter.Year.Value > 0 ? filter.Year.Value : DateTime.Now.Year;
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : filter.PageSize;

        var parameters = new DynamicParameters();
        parameters.Add("Year", targetYear);

        var effectiveUnitId = isAdmin ? filter.UnitId : (filter.UnitId ?? userUnitId);

        var baseWhere = @"
            d.IsDeleted = 0
            AND d.STATUS_ID = 6
            AND d.PUBLISHSTATUSID = 2
            AND EXTRACT(YEAR FROM d.CreatedDate) = :Year";

        if (effectiveUnitId.HasValue)
        {
            baseWhere += @" AND i.UNIT_ID IN (
                SELECT Id FROM ORGANIZATION_UNIT START WITH Id = :UnitId CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", effectiveUnitId.Value);
        }

        AppendObjectTypeFilterToWhere(filter.ObjectType, ref baseWhere);
        AppendEquipmentTypeFilterToWhere(filter.EquipmentTypeIds, ref baseWhere, parameters);

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
}
