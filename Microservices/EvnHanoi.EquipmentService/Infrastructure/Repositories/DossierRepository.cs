using System.Data;
using System.Text.Json;
using Dapper;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;
using InfrastructureEntity = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;
using GridTypeEntity = EvnHanoi.EquipmentService.Core.Entities.GridType;
namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class DossierRepository : IDossierRepository
{
    private readonly IDbConnection _connection;
    public DossierRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync(IEnumerable<long>? authorizedUnitIds = null)
    {
        _connection.EnsureOpen();

        var sql = @"SELECT ID, CODE, NAME, INFRA_TYPE_ID as InfraTypeId, UNIT_ID as UnitId, GRIDTYPEID as GridTypeId, IS_ACTIVE as IsActive 
                    FROM INFRASTRUCTURE 
                    WHERE IsDeleted = 0";

        var parameters = new DynamicParameters();
        if (authorizedUnitIds != null && authorizedUnitIds.Any())
        {
            sql += " AND UNIT_ID IN :AuthorizedUnitIds";
            parameters.Add("AuthorizedUnitIds", authorizedUnitIds.ToArray());
        }

        sql += " ORDER BY NAME ASC";
        return await _connection.QueryAsync<InfrastructureEntity>(sql, parameters);
    }

    public async Task<IEnumerable<GridTypeEntity>> GetGridTypesLookupAsync()
    {
        _connection.EnsureOpen();

        var sql = "SELECT Id, Name FROM GridTypes ORDER BY Id ASC";
        return await _connection.QueryAsync<GridTypeEntity>(sql);
    }
    public async Task<IEnumerable<DossierType>> GetDossierTypesLookupAsync()
    {
        _connection.EnsureOpen();

        var sql = @"SELECT Id,
                           Name,
                           Code,
                           FORM_ID  AS FormId,
                           IS_ACTIVE AS IsActive,
                           PIORITY   AS Piority
                    FROM DOSSIER_TYPES
                    WHERE IsDeleted = 0
                      AND IS_ACTIVE = 1
                    ORDER BY PIORITY ASC, Id ASC";
        return await _connection.QueryAsync<DossierType>(sql);
    }

    public async Task<IEnumerable<DossierGroup>> GetDossierGroupsLookupAsync()
    {
        _connection.EnsureOpen();
        const string sql = @"SELECT ID as Id,
                                    CODE as Code,
                                    NAME as Name,
                                    INFRA_TYPE_ID as InfraTypeId,
                                    IS_EQUIPMENT_DOSSIER as IsEquipmentDossier
                             FROM DOSSIER_GROUPS
                             ORDER BY ID ASC";
        return await _connection.QueryAsync<DossierGroup>(sql);
    }

    public async Task<DossierGroup?> GetDossierGroupByIdAsync(int id)
    {
        _connection.EnsureOpen();
        const string sql = @"SELECT ID as Id,
                                    CODE as Code,
                                    NAME as Name,
                                    INFRA_TYPE_ID as InfraTypeId,
                                    IS_EQUIPMENT_DOSSIER as IsEquipmentDossier
                             FROM DOSSIER_GROUPS
                             WHERE ID = :Id";
        return await _connection.QuerySingleOrDefaultAsync<DossierGroup>(sql, new { Id = id });
    }

    [Obsolete("Dùng IDossierSearchRepository qua DossierService.GetPagedAsync.")]
    public Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(DossierFilterDto filter)
        => throw new NotSupportedException("Danh sách hồ sơ đã chuyển sang Elasticsearch. Gọi DossierService.GetPagedAsync.");

    private class BhsCatalogDefinition
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    private async Task<IReadOnlyList<BhsCatalogDefinition>> GetBhsCatalogDefinitionsAsync()
    {
        const string sql = @"
            SELECT c.Code, c.Name, c.Priority
            FROM CATALOG c
            INNER JOIN CATALOG_TYPE ct ON c.CatalogTypeId = ct.Id
            WHERE ct.Code = 'BHS'
              AND c.IsDeleted = 0
              AND ct.IsDeleted = 0
            ORDER BY c.Priority ASC, c.Name ASC";
        return (await _connection.QueryAsync<BhsCatalogDefinition>(sql)).ToList();
    }

    private static Dictionary<string, string> ParseCatalogData(string? formDataJson, IReadOnlyList<BhsCatalogDefinition> bhsCatalogs)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(formDataJson))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(formDataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var catalog in bhsCatalogs)
            {
                if (doc.RootElement.TryGetProperty(catalog.Code, out var prop) || 
                    doc.RootElement.TryGetProperty(catalog.Name, out prop))
                {
                    var val = prop.ValueKind switch
                    {
                        JsonValueKind.String => prop.GetString() ?? string.Empty,
                        JsonValueKind.Number => prop.GetRawText(),
                        JsonValueKind.True or JsonValueKind.False => prop.GetBoolean().ToString(),
                        JsonValueKind.Null => string.Empty,
                        _ => prop.GetRawText()
                    };

                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        result[catalog.Name] = val;
                    }
                }
            }
        }
        catch
        {
            // Bỏ qua lỗi cú pháp JSON
        }

        return result;
    }

    public async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetCatalogDossiersAsync(
        string? keyword,
        Guid? infrastructureId,
        Guid? dossierTypeId,
        long? unitId,
        int page,
        int pageSize)
    {
        _connection.EnsureOpen();

        var parameters = new DynamicParameters();
        var sqlBase = $@"FROM DOSSIERS d
                         LEFT JOIN INFRASTRUCTURE i ON d.{nameof(Dossier.InfrastructureId)} = i.ID
                         LEFT JOIN DOSSIER_TYPES dt ON d.{nameof(Dossier.DossierTypeId)} = dt.ID
                         LEFT JOIN DOSSIER_SETS ds ON d.{nameof(Dossier.DossierSetId)} = ds.ID
                         LEFT JOIN PUBLISH_STATUSES ps ON d.PUBLISHSTATUSID = ps.ID
                         LEFT JOIN DOSSIER_STATUSES dstat ON d.STATUS_ID = dstat.ID
                         WHERE d.{nameof(Dossier.IsDeleted)} = 0 AND d.PUBLISHSTATUSID = 2";

        if (infrastructureId.HasValue)
        {
            sqlBase += $" AND d.{nameof(Dossier.InfrastructureId)} = :InfrastructureId";
            parameters.Add("InfrastructureId", infrastructureId.Value.ToString());
        }

        if (dossierTypeId.HasValue)
        {
            sqlBase += $" AND d.{nameof(Dossier.DossierTypeId)} = :DossierTypeId";
            parameters.Add("DossierTypeId", dossierTypeId.Value.ToString());
        }

        if (unitId.HasValue)
        {
            sqlBase += @" AND i.UNIT_ID IN (
                SELECT Id 
                FROM ORGANIZATION_UNIT
                START WITH Id = :UnitId
                CONNECT BY PRIOR Id = ParentId
            )";
            parameters.Add("UnitId", unitId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sqlBase += $" AND UPPER(d.{nameof(Dossier.FormDataJson)}) LIKE :Keyword";
            parameters.Add("Keyword", $"%{keyword.ToUpper().Trim()}%");
        }

        var countSql = $"SELECT COUNT(1) {sqlBase}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        if (totalCount == 0)
        {
            return (Enumerable.Empty<DossierListItemDto>(), 0);
        }

        var selectSql = $@"SELECT
                            d.{nameof(Dossier.Id)},
                            d.{nameof(Dossier.GridTypeId)},
                            d.{nameof(Dossier.InfrastructureId)},
                            i.NAME as {nameof(DossierListItemDto.InfrastructureName)},
                            i.CODE as {nameof(DossierListItemDto.InfrastructureCode)},
                            d.{nameof(Dossier.DossierSetId)},
                            ds.NAME as {nameof(DossierListItemDto.DossierSetName)},
                            d.{nameof(Dossier.DossierTypeId)},
                            dt.NAME as {nameof(DossierListItemDto.DossierTypeName)},
                            d.STATUS_ID as StatusId,
                            dstat.CODE as StatusCode,
                            dstat.NAME as StatusName,
                            d.{nameof(Dossier.WorkflowStatusName)},
                            d.{nameof(Dossier.CreatorName)},
                            d.{nameof(Dossier.CreatedDate)},
                            d.{nameof(Dossier.FormDataJson)},
                            d.PUBLISHSTATUSID as PublishStatusId,
                            ps.CODE as PublishStatusCode,
                            ps.NAME as PublishStatusName,
                            (SELECT COUNT(1) FROM DOCUMENTS doc WHERE doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0) as {nameof(DossierListItemDto.DocumentCount)}
                         {sqlBase}
                         ORDER BY d.{nameof(Dossier.CreatedDate)} DESC
                         OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var rawItems = await _connection.QueryAsync<dynamic>(selectSql, parameters);
        var mappedItems = rawItems.Select(d => (
            dto: new DossierListItemDto
            {
                Id = d.ID is string sId && Guid.TryParse(sId, out var gId) ? gId : (d.ID is Guid guidId ? guidId : Guid.Empty),
                GridTypeId = d.GRIDTYPEID == null ? (int?)null : Convert.ToInt32(d.GRIDTYPEID),
                GridTypeName = null,
                InfrastructureId = d.INFRASTRUCTUREID is string sInfra && Guid.TryParse(sInfra, out var gInfra) ? gInfra : (d.INFRASTRUCTUREID is Guid guidInfra ? guidInfra : null),
                InfrastructureName = d.INFRASTRUCTURENAME,
                InfrastructureCode = d.INFRASTRUCTURECODE,
                DossierSetId = d.DOSSIERSETID is string sSet && Guid.TryParse(sSet, out var gSet) ? gSet : (d.DOSSIERSETID is Guid guidSet ? guidSet : null),
                DossierSetName = d.DOSSIERSETNAME,
                DossierTypeId = d.DOSSIERTYPEID is string sType && Guid.TryParse(sType, out var gType) ? gType : (d.DOSSIERTYPEID is Guid guidType ? guidType : Guid.Empty),
                DossierTypeName = d.DOSSIERTYPENAME,
                StatusId = d.STATUSID == null ? 0 : Convert.ToInt32(d.STATUSID),
                StatusCode = d.STATUSCODE,
                StatusName = d.STATUSNAME,
                WorkflowStatusName = d.WORKFLOWSTATUSNAME,
                CreatedDate = d.CREATEDDATE is DateTime dtVal ? dtVal : DateTime.MinValue,
                DocumentCount = d.DOCUMENTCOUNT == null ? 0 : Convert.ToInt32(d.DOCUMENTCOUNT),
                PublishStatusId = d.PUBLISHSTATUSID == null ? (int?)null : Convert.ToInt32(d.PUBLISHSTATUSID),
                PublishStatusCode = d.PUBLISHSTATUSCODE,
                PublishStatusName = d.PUBLISHSTATUSNAME
            },
            Item2: d.FORMDATAJSON as string
        )).ToList();

        var bhsCatalogs = await GetBhsCatalogDefinitionsAsync();
        var resultList = new List<DossierListItemDto>();
        foreach (var tuple in mappedItems)
        {
            tuple.dto.CatalogData = ParseCatalogData(tuple.Item2, bhsCatalogs);
            resultList.Add(tuple.dto);
        }

        return (resultList, totalCount);
    }

    public async Task<IEnumerable<BhsCatalogColumnDto>> GetBhsCatalogColumnsAsync()
    {
        _connection.EnsureOpen();

        var bhsCatalogs = await GetBhsCatalogDefinitionsAsync();
        return bhsCatalogs.Select(c => new BhsCatalogColumnDto
        {
            Key = c.Name,
            Code = c.Code,
            Label = c.Name,
            Priority = c.Priority
        });
    }

    public Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupInfrastructuresAsync(
        DossierByEquipmentFilterDto filter,
        long? unitId) =>
        QueryEquipmentLookupAsync(
            filter,
            unitId,
            """
            SELECT DISTINCT i.ID AS Id, i.NAME AS Name, i.CODE AS Code
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            """,
            excludeInfrastructure: true);

    public Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupEquipmentTypesAsync(
        DossierByEquipmentFilterDto filter,
        long? unitId) =>
        QueryEquipmentLookupAsync(
            filter,
            unitId,
            """
            SELECT DISTINCT et.ID AS Id, et.NAME AS Name, et.CODE AS Code
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
            INNER JOIN Equipments e ON de.EquipmentId = e.Id AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
            INNER JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
            """,
            keywordFields: "et",
            excludeEquipmentType: true);

    public Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupEquipmentsAsync(
        DossierByEquipmentFilterDto filter,
        long? unitId) =>
        QueryEquipmentLookupAsync(
            filter,
            unitId,
            """
            SELECT DISTINCT e.ID AS Id, e.NAME AS Name, e.CODE AS Code
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
            INNER JOIN Equipments e ON de.EquipmentId = e.Id AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
            """,
            keywordFields: "e",
            excludeEquipment: true);

    public Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupDossierTypesAsync(
        DossierByEquipmentFilterDto filter,
        long? unitId) =>
        QueryEquipmentLookupAsync(
            filter,
            unitId,
            """
            SELECT DISTINCT dt.ID AS Id, dt.NAME AS Name, dt.CODE AS Code
            FROM DOSSIERS d
            INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
            INNER JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            """,
            keywordFields: "dt",
            excludeDossierType: true);

    public async Task<bool> IsPublishedDossierAccessibleAsync(Guid dossierId, long? unitId)
    {
        _connection.EnsureOpen();

        var parameters = new DynamicParameters();
        parameters.Add("DossierId", dossierId.ToString());

        var sql = @"SELECT COUNT(1)
                    FROM DOSSIERS d
                    INNER JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                    WHERE d.Id = :DossierId
                      AND d.IsDeleted = 0
                      AND d.STATUS_ID = 6
                      AND d.PUBLISHSTATUSID = 2";

        AppendUnitScope(sql, parameters, unitId, out sql);

        var count = await _connection.ExecuteScalarAsync<int>(sql, parameters);
        return count > 0;
    }

    private async Task<IEnumerable<DossierByEquipmentLookupItemDto>> QueryEquipmentLookupAsync(
        DossierByEquipmentFilterDto filter,
        long? unitId,
        string selectFromSql,
        string keywordFields = "i",
        bool excludeInfrastructure = false,
        bool excludeEquipmentType = false,
        bool excludeEquipment = false,
        bool excludeDossierType = false)
    {
        _connection.EnsureOpen();

        var parameters = new DynamicParameters();
        var sql = selectFromSql + @"
                    WHERE d.IsDeleted = 0
                      AND d.STATUS_ID = 6
                      AND d.PUBLISHSTATUSID = 2";

        AppendPublishedDossierFilters(
            ref sql,
            parameters,
            filter,
            unitId,
            excludeInfrastructure,
            excludeEquipmentType,
            excludeEquipment,
            excludeDossierType);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = $"%{filter.Keyword.Trim().ToUpperInvariant()}%";
            parameters.Add("LookupKeyword", keyword);
            sql += keywordFields switch
            {
                "et" => " AND (UPPER(et.NAME) LIKE :LookupKeyword OR UPPER(et.CODE) LIKE :LookupKeyword)",
                "e" => " AND (UPPER(e.NAME) LIKE :LookupKeyword OR UPPER(e.CODE) LIKE :LookupKeyword OR UPPER(e.SerialNumber) LIKE :LookupKeyword)",
                "dt" => " AND (UPPER(dt.NAME) LIKE :LookupKeyword OR UPPER(dt.CODE) LIKE :LookupKeyword)",
                _ => " AND (UPPER(i.NAME) LIKE :LookupKeyword OR UPPER(i.CODE) LIKE :LookupKeyword)"
            };
        }

        sql += " ORDER BY Name ASC";

        var rows = await _connection.QueryAsync<DossierByEquipmentLookupItemDto>(sql, parameters);
        return rows.Select(row => new DossierByEquipmentLookupItemDto
        {
            Id = row.Id,
            Name = row.Name,
            Code = row.Code
        });
    }

    private static void AppendPublishedDossierFilters(
        ref string sql,
        DynamicParameters parameters,
        DossierByEquipmentFilterDto filter,
        long? unitId,
        bool excludeInfrastructure,
        bool excludeEquipmentType,
        bool excludeEquipment,
        bool excludeDossierType)
    {
        AppendUnitScope(sql, parameters, unitId, out sql);

        if (filter.PublishDateFrom.HasValue)
        {
            sql += " AND d.ModifiedDate >= :PublishDateFrom";
            parameters.Add("PublishDateFrom", filter.PublishDateFrom.Value);
        }

        if (filter.PublishDateTo.HasValue)
        {
            sql += " AND d.ModifiedDate <= :PublishDateTo";
            parameters.Add("PublishDateTo", filter.PublishDateTo.Value);
        }

        if (filter.GridTypeId.HasValue)
        {
            sql += " AND d.GridTypeId = :GridTypeId";
            parameters.Add("GridTypeId", filter.GridTypeId.Value);
        }

        if (!excludeInfrastructure && filter.InfrastructureId.HasValue)
        {
            sql += " AND d.InfrastructureId = :InfrastructureId";
            parameters.Add("InfrastructureId", filter.InfrastructureId.Value.ToString());
        }

        if (!excludeDossierType && filter.DossierTypeId.HasValue)
        {
            sql += " AND d.DossierTypeId = :DossierTypeId";
            parameters.Add("DossierTypeId", filter.DossierTypeId.Value.ToString());
        }

        if (!excludeEquipment && filter.EquipmentId.HasValue)
        {
            sql += @" AND EXISTS (
                SELECT 1 FROM DOSSIER_EQUIPMENTS de2
                WHERE de2.DossierId = d.Id AND de2.EquipmentId = :EquipmentId
            )";
            parameters.Add("EquipmentId", filter.EquipmentId.Value.ToString());
        }
        else if (!excludeEquipmentType && filter.EquipmentTypeId.HasValue)
        {
            sql += @" AND EXISTS (
                SELECT 1
                FROM DOSSIER_EQUIPMENTS de2
                INNER JOIN Equipments e2 ON de2.EquipmentId = e2.Id
                WHERE de2.DossierId = d.Id
                  AND e2.EquipmentTypeId = :EquipmentTypeId
                  AND (e2.IsDeleted = 0 OR e2.IsDeleted IS NULL)
            )";
            parameters.Add("EquipmentTypeId", filter.EquipmentTypeId.Value.ToString());
        }
    }

    private static void AppendUnitScope(string sql, DynamicParameters parameters, long? unitId, out string resultSql)
    {
        resultSql = sql;
        if (!unitId.HasValue)
            return;

        resultSql += @" AND i.UNIT_ID IN (
            SELECT Id
            FROM ORGANIZATION_UNIT
            START WITH Id = :UnitId
            CONNECT BY PRIOR Id = ParentId
        )";
        parameters.Add("UnitId", unitId.Value);
    }

    public async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount, IEnumerable<BhsCatalogColumnDto> Columns)> GetDossiersByEquipmentAsync(
        Guid equipmentId,
        int page,
        int pageSize)
    {
        _connection.EnsureOpen();

        // Get columns first
        var bhsCatalogs = await GetBhsCatalogDefinitionsAsync();
        var columns = bhsCatalogs.Select(c => new BhsCatalogColumnDto
        {
            Key = c.Name,
            Code = c.Code,
            Label = c.Name,
            Priority = c.Priority
        }).ToList();

        var parameters = new DynamicParameters();
        parameters.Add("EquipmentId", equipmentId.ToString());

        var sqlBase = $@"FROM DOSSIERS d
                         INNER JOIN DOSSIER_EQUIPMENTS de ON d.Id = de.DossierId
                         LEFT JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                         LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
                         LEFT JOIN DOSSIER_SETS ds ON d.DossierSetId = ds.ID
                         LEFT JOIN PUBLISH_STATUSES ps ON d.PUBLISHSTATUSID = ps.ID
                         LEFT JOIN DOSSIER_STATUSES dstat ON d.STATUS_ID = dstat.ID
                         WHERE d.IsDeleted = 0
                           AND d.PUBLISHSTATUSID = 2
                           AND de.EquipmentId = :EquipmentId";

        var countSql = $"SELECT COUNT(1) {sqlBase}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        if (totalCount == 0)
        {
            return (Enumerable.Empty<DossierListItemDto>(), 0, columns);
        }

        var selectSql = $@"SELECT
                            d.Id,
                            d.GridTypeId,
                            d.InfrastructureId,
                            i.NAME as InfrastructureName,
                            i.CODE as InfrastructureCode,
                            d.DossierSetId,
                            ds.NAME as DossierSetName,
                            d.DossierTypeId,
                            dt.NAME as DossierTypeName,
                            d.STATUS_ID as StatusId,
                            dstat.CODE as StatusCode,
                            dstat.NAME as StatusName,
                            d.WorkflowStatusName,
                            d.CreatorName,
                            d.CreatedDate,
                            d.FormDataJson,
                            d.PUBLISHSTATUSID as PublishStatusId,
                            ps.CODE as PublishStatusCode,
                            ps.NAME as PublishStatusName,
                            (SELECT COUNT(1) FROM DOCUMENTS doc WHERE doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0) as DocumentCount
                         {sqlBase}
                         ORDER BY d.CreatedDate DESC
                         OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var rawItems = await _connection.QueryAsync<dynamic>(selectSql, parameters);
        var mappedItems = rawItems.Select(d => (
            dto: new DossierListItemDto
            {
                Id = d.ID is string sId && Guid.TryParse(sId, out var gId) ? gId : (d.ID is Guid guidId ? guidId : Guid.Empty),
                GridTypeId = d.GRIDTYPEID == null ? (int?)null : Convert.ToInt32(d.GRIDTYPEID),
                GridTypeName = null,
                InfrastructureId = d.INFRASTRUCTUREID is string sInfra && Guid.TryParse(sInfra, out var gInfra) ? gInfra : (d.INFRASTRUCTUREID is Guid guidInfra ? guidInfra : null),
                InfrastructureName = d.INFRASTRUCTURENAME,
                InfrastructureCode = d.INFRASTRUCTURECODE,
                DossierSetId = d.DOSSIERSETID is string sSet && Guid.TryParse(sSet, out var gSet) ? gSet : (d.DOSSIERSETID is Guid guidSet ? guidSet : null),
                DossierSetName = d.DOSSIERSETNAME,
                DossierTypeId = d.DOSSIERTYPEID is string sType && Guid.TryParse(sType, out var gType) ? gType : (d.DOSSIERTYPEID is Guid guidType ? guidType : Guid.Empty),
                DossierTypeName = d.DOSSIERTYPENAME,
                StatusId = d.STATUSID == null ? 0 : Convert.ToInt32(d.STATUSID),
                StatusCode = d.STATUSCODE,
                StatusName = d.STATUSNAME,
                WorkflowStatusName = d.WORKFLOWSTATUSNAME,
                CreatedDate = d.CREATEDDATE is DateTime dtVal ? dtVal : DateTime.MinValue,
                DocumentCount = d.DOCUMENTCOUNT == null ? 0 : Convert.ToInt32(d.DOCUMENTCOUNT),
                PublishStatusId = d.PUBLISHSTATUSID == null ? (int?)null : Convert.ToInt32(d.PUBLISHSTATUSID),
                PublishStatusCode = d.PUBLISHSTATUSCODE,
                PublishStatusName = d.PUBLISHSTATUSNAME
            },
            Item2: d.FORMDATAJSON as string
        )).ToList();

        var resultList = new List<DossierListItemDto>();
        foreach (var tuple in mappedItems)
        {
            tuple.dto.CatalogData = ParseCatalogData(tuple.Item2, bhsCatalogs);
            resultList.Add(tuple.dto);
        }

        return (resultList, totalCount, columns);
    }

    public async Task<DossierDetailDto?> GetDetailByIdAsync(Guid id)
    {
        return await _connection.ExecuteWithRetryAsync(async conn =>
        {
            var sql = $@"SELECT
                        d.{nameof(Dossier.Id)},
                        d.DOSSIER_GROUP_ID as {nameof(DossierDetailDto.DossierGroupId)},
                        dg.NAME as {nameof(DossierDetailDto.DossierGroupName)},
                        dg.IS_EQUIPMENT_DOSSIER as {nameof(DossierDetailDto.IsEquipmentDossier)},
                        d.{nameof(Dossier.GridTypeId)},
                        gt.Name as {nameof(DossierDetailDto.GridTypeName)},
                        d.{nameof(Dossier.InfrastructureId)},
                        i.NAME as {nameof(DossierDetailDto.InfrastructureName)},
                        i.CODE as {nameof(DossierDetailDto.InfrastructureCode)},
                        d.{nameof(Dossier.DossierSetId)},
                        ds.NAME as {nameof(DossierDetailDto.DossierSetName)},
                        d.{nameof(Dossier.DossierTypeId)},
                        dt.NAME as {nameof(DossierDetailDto.DossierTypeName)},
                        dt.FORM_ID as {nameof(DossierDetailDto.FormId)},
                        d.{nameof(Dossier.FormDataJson)},
                        d.STATUS_ID as StatusId,
                        dstat.CODE as StatusCode,
                        dstat.NAME as StatusName,
                        d.KIND_ID as {nameof(DossierDetailDto.KindId)},
                        d.{nameof(Dossier.WorkflowInstanceId)},
                        d.{nameof(Dossier.WorkflowStatusName)},
                        d.{nameof(Dossier.RowVersion)},
                        d.{nameof(Dossier.CreatedBy)},
                        d.{nameof(Dossier.CreatedDate)},
                        d.{nameof(Dossier.ModifiedBy)},
                        d.{nameof(Dossier.ModifiedDate)},
                        d.PUBLISHSTATUSID as {nameof(DossierDetailDto.PublishStatusId)},
                        ps.CODE as {nameof(DossierDetailDto.PublishStatusCode)},
                        ps.NAME as {nameof(DossierDetailDto.PublishStatusName)},
                        d.ShelfId as {nameof(DossierDetailDto.ShelfId)},
                        shel.Code as {nameof(DossierDetailDto.ShelfCode)},
                        shel.Name as {nameof(DossierDetailDto.ShelfName)},
                        d.FloorId as {nameof(DossierDetailDto.FloorId)},
                        fl.Code as {nameof(DossierDetailDto.FloorCode)},
                        fl.Name as {nameof(DossierDetailDto.FloorName)},
                        d.BoxId as {nameof(DossierDetailDto.BoxId)},
                        bx.Code as {nameof(DossierDetailDto.BoxCode)},
                        bx.Name as {nameof(DossierDetailDto.BoxName)},
                        d.{nameof(Dossier.CreatorId)} as Id,
                        d.{nameof(Dossier.CreatorUsername)} as Username,
                        d.{nameof(Dossier.CreatorName)} as Name
                     FROM DOSSIERS d
                     LEFT JOIN DOSSIER_GROUPS dg ON d.DOSSIER_GROUP_ID = dg.ID
                     LEFT JOIN GridTypes gt ON d.{nameof(Dossier.GridTypeId)} = gt.Id
                     LEFT JOIN INFRASTRUCTURE i ON d.{nameof(Dossier.InfrastructureId)} = i.ID
                     LEFT JOIN DOSSIER_TYPES dt ON d.{nameof(Dossier.DossierTypeId)} = dt.ID
                     LEFT JOIN DOSSIER_SETS ds ON d.{nameof(Dossier.DossierSetId)} = ds.ID
                     LEFT JOIN PUBLISH_STATUSES ps ON d.PUBLISHSTATUSID = ps.ID
                     LEFT JOIN DOSSIER_STATUSES dstat ON d.STATUS_ID = dstat.ID
                     LEFT JOIN PHYSICAL_SHELF shel ON d.ShelfId = shel.Id
                     LEFT JOIN PHYSICAL_FLOOR fl ON d.FloorId = fl.Id
                     LEFT JOIN PHYSICAL_BOX bx ON d.BoxId = bx.Id
                     WHERE d.{nameof(Dossier.Id)} = :Id AND d.{nameof(Dossier.IsDeleted)} = 0";
            var dossierList = await conn.QueryAsync<DossierDetailDto, CreatorInfoDto, DossierDetailDto>(
                sql,
                (dossierDto, creatorDto) =>
                {
                    dossierDto.Creator = creatorDto;
                    return dossierDto;
                },
                new { Id = id.ToString() },
                splitOn: "Id"
            );
            var dossier = dossierList.FirstOrDefault();
            if (dossier == null) return null;

            dossier.Equipments = (await GetEquipmentsAsync(id)).ToList();
            return dossier;
        });
    }
    public async Task<Dossier?> GetByIdAsync(Guid id)
    {
        _connection.EnsureOpen();
        var sql = $@"SELECT Id, DOSSIER_GROUP_ID as DossierGroupId, GridTypeId, InfrastructureId, DossierSetId, DossierTypeId, FormDataJson, STATUS_ID as StatusId, KIND_ID as KindId, WorkflowInstanceId, WorkflowStatusName, RowVersion, CreatorId, CreatorUsername, CreatorName, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate, IsDeleted, PUBLISHSTATUSID as PublishStatusId, ShelfId, FloorId, BoxId FROM DOSSIERS WHERE {nameof(Dossier.Id)} = :Id AND {nameof(Dossier.IsDeleted)} = 0";
        return await _connection.QuerySingleOrDefaultAsync<Dossier>(sql, new { Id = id.ToString() });
    }

    public async Task<int?> GetKindIdAsync(Guid id)
    {
        _connection.EnsureOpen();
        const string sql = "SELECT KIND_ID FROM DOSSIERS WHERE Id = :Id AND IsDeleted = 0";
        return await _connection.QuerySingleOrDefaultAsync<int?>(sql, new { Id = id.ToString() });
    }
    public async Task<Guid> CreateAsync(Dossier dossier, IEnumerable<Guid> equipmentIds)
    {
        _connection.EnsureOpen();
        if (dossier.Id == Guid.Empty)
            dossier.Id = Guid.Parse(UuidHelper.NewUuid());
        using var transaction = _connection.BeginTransaction();
        try
        {
            var sql = $@"INSERT INTO DOSSIERS (
                            {nameof(Dossier.Id)},
                            DOSSIER_GROUP_ID,
                            {nameof(Dossier.GridTypeId)},
                            {nameof(Dossier.InfrastructureId)},
                            {nameof(Dossier.DossierSetId)},
                            {nameof(Dossier.DossierTypeId)},
                            {nameof(Dossier.FormDataJson)},
                            STATUS_ID,
                            KIND_ID,
                            {nameof(Dossier.RowVersion)},
                            {nameof(Dossier.CreatorId)},
                            {nameof(Dossier.CreatorUsername)},
                            {nameof(Dossier.CreatorName)},
                            {nameof(Dossier.CreatedBy)},
                            {nameof(Dossier.CreatedDate)},
                            {nameof(Dossier.IsDeleted)},
                            {nameof(Dossier.ShelfId)},
                            {nameof(Dossier.FloorId)},
                            {nameof(Dossier.BoxId)}
                        ) VALUES (
                            :Id, :DossierGroupId, :GridTypeId, :InfrastructureId, :DossierSetId, :DossierTypeId,
                            :FormDataJson, :StatusId, :KindId, :RowVersion, :CreatorId, :CreatorUsername,
                            :CreatorName, :CreatedBy, :CreatedDate, :IsDeleted,
                            :ShelfId, :FloorId, :BoxId
                        )";
            await _connection.ExecuteAsync(sql, new
            {
                Id = dossier.Id.ToString(),
                dossier.DossierGroupId,
                dossier.GridTypeId,
                InfrastructureId = dossier.InfrastructureId?.ToString(),
                DossierSetId = dossier.DossierSetId?.ToString(),
                DossierTypeId = dossier.DossierTypeId.ToString(),
                FormDataJson = OracleClob.Param(dossier.FormDataJson),
                dossier.StatusId,
                dossier.KindId,
                dossier.RowVersion,
                CreatorId = dossier.CreatorId?.ToString(),
                dossier.CreatorUsername,
                dossier.CreatorName,
                dossier.CreatedBy,
                dossier.CreatedDate,
                IsDeleted = dossier.IsDeleted ? 1 : 0,
                dossier.ShelfId,
                dossier.FloorId,
                dossier.BoxId
            }, transaction);
            // Insert equipment links
            foreach (var equipId in equipmentIds)
            {
                await _connection.ExecuteAsync(
                    "INSERT INTO DOSSIER_EQUIPMENTS (DossierId, EquipmentId) VALUES (:DossierId, :EquipmentId)",
                    new { DossierId = dossier.Id.ToString(), EquipmentId = equipId.ToString() },
                    transaction);
            }
            transaction.Commit();
            return dossier.Id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    public async Task<bool> UpdateAsync(Dossier dossier, IEnumerable<Guid> equipmentIds)
    {
        _connection.EnsureOpen();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var sql = $@"UPDATE DOSSIERS SET
                            DOSSIER_GROUP_ID = :DossierGroupId,
                            {nameof(Dossier.GridTypeId)} = :GridTypeId,
                            {nameof(Dossier.InfrastructureId)} = :InfrastructureId,
                            {nameof(Dossier.DossierSetId)} = :DossierSetId,
                            {nameof(Dossier.DossierTypeId)} = :DossierTypeId,
                            {nameof(Dossier.FormDataJson)} = :FormDataJson,
                            {nameof(Dossier.ShelfId)} = :ShelfId,
                            {nameof(Dossier.FloorId)} = :FloorId,
                            {nameof(Dossier.BoxId)} = :BoxId,
                            {nameof(Dossier.ModifiedBy)} = :ModifiedBy,
                            {nameof(Dossier.ModifiedDate)} = :ModifiedDate,
                            {nameof(Dossier.RowVersion)} = {nameof(Dossier.RowVersion)} + 1
                         WHERE {nameof(Dossier.Id)} = :Id
                           AND {nameof(Dossier.RowVersion)} = :RowVersion
                           AND {nameof(Dossier.IsDeleted)} = 0";
            var affected = await _connection.ExecuteAsync(sql, new
            {
                Id = dossier.Id.ToString(),
                dossier.DossierGroupId,
                dossier.GridTypeId,
                InfrastructureId = dossier.InfrastructureId?.ToString(),
                DossierSetId = dossier.DossierSetId?.ToString(),
                DossierTypeId = dossier.DossierTypeId.ToString(),
                FormDataJson = OracleClob.Param(dossier.FormDataJson),
                dossier.ShelfId,
                dossier.FloorId,
                dossier.BoxId,
                dossier.ModifiedBy,
                dossier.ModifiedDate,
                dossier.RowVersion
            }, transaction);
            if (affected == 0)
            {
                transaction.Rollback();
                throw new Exception("Concurrency conflict: Hồ sơ đã được cập nhật bởi người dùng khác.");
            }
            // Update equipment list: xóa cũ, thêm mới
            await _connection.ExecuteAsync(
                "DELETE FROM DOSSIER_EQUIPMENTS WHERE DossierId = :DossierId",
                new { DossierId = dossier.Id.ToString() }, transaction);
            foreach (var equipId in equipmentIds)
            {
                await _connection.ExecuteAsync(
                    "INSERT INTO DOSSIER_EQUIPMENTS (DossierId, EquipmentId) VALUES (:DossierId, :EquipmentId)",
                    new { DossierId = dossier.Id.ToString(), EquipmentId = equipId.ToString() },
                    transaction);
            }
            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    public async Task<bool> SoftDeleteAsync(Guid id, string modifiedBy)
    {
        _connection.EnsureOpen();
        var sql = $@"UPDATE DOSSIERS SET
                        {nameof(Dossier.IsDeleted)} = 1,
                        {nameof(Dossier.ModifiedBy)} = :ModifiedBy,
                        {nameof(Dossier.ModifiedDate)} = :ModifiedDate
                     WHERE {nameof(Dossier.Id)} = :Id AND {nameof(Dossier.IsDeleted)} = 0";
        var affected = await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            ModifiedBy = modifiedBy,
            ModifiedDate = DateTime.UtcNow
        });
        return affected > 0;
    }
    public async Task<bool> UpdateStatusAsync(Guid id, int statusId, string modifiedBy)
    {
        _connection.EnsureOpen();
        var sql = $@"UPDATE DOSSIERS SET
                        STATUS_ID = :StatusId,
                        {nameof(Dossier.ModifiedBy)} = :ModifiedBy,
                        {nameof(Dossier.ModifiedDate)} = :ModifiedDate
                     WHERE {nameof(Dossier.Id)} = :Id AND {nameof(Dossier.IsDeleted)} = 0";
        var affected = await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            StatusId = statusId,
            ModifiedBy = modifiedBy,
            ModifiedDate = DateTime.UtcNow
        });
        return affected > 0;
    }
    public async Task<bool> UpdateWorkflowAsync(Guid id, Guid workflowInstanceId, string workflowStatusName, int statusId, int? publishStatusId, string modifiedBy)
    {
        _connection.EnsureOpen();
        var sql = $@"UPDATE DOSSIERS SET
                        {nameof(Dossier.WorkflowInstanceId)} = :WorkflowInstanceId,
                        {nameof(Dossier.WorkflowStatusName)} = :WorkflowStatusName,
                        STATUS_ID = :StatusId,
                        {nameof(Dossier.ModifiedBy)} = :ModifiedBy,
                        {nameof(Dossier.ModifiedDate)} = :ModifiedDate";
        if (publishStatusId.HasValue)
        {
            sql += ", PUBLISHSTATUSID = :PublishStatusId";
        }
        sql += $" WHERE {nameof(Dossier.Id)} = :Id AND {nameof(Dossier.IsDeleted)} = 0";

        var affected = await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            WorkflowInstanceId = workflowInstanceId.ToString(),
            WorkflowStatusName = workflowStatusName,
            StatusId = statusId,
            PublishStatusId = publishStatusId,
            ModifiedBy = modifiedBy,
            ModifiedDate = DateTime.UtcNow
        });
        return affected > 0;
    }

    public async Task<bool> UpdatePublishStatusAsync(Guid id, int publishStatusId, string modifiedBy)
    {
        _connection.EnsureOpen();
        const string sql = @"UPDATE DOSSIERS SET 
                               PUBLISHSTATUSID = :PublishStatusId,
                               ModifiedBy = :ModifiedBy,
                               ModifiedDate = :ModifiedDate
                             WHERE Id = :Id AND IsDeleted = 0";
        var affected = await _connection.ExecuteAsync(sql, new 
        { 
            Id = id.ToString(), 
            PublishStatusId = publishStatusId, 
            ModifiedBy = modifiedBy, 
            ModifiedDate = DateTime.UtcNow 
        });
        return affected > 0;
    }
    public async Task<bool> SaveActiveWorkflowTaskAsync(Guid dossierId, string stepId, string stepName, string assignees, string actionsJson, string modifiedBy)
    {
        _connection.EnsureOpen();

        using var transaction = _connection.BeginTransaction();
        try
        {
            var deleteSql = "DELETE FROM WORKFLOW_TASKS_ACTIVE WHERE DOSSIER_ID = :DossierId";
            await _connection.ExecuteAsync(deleteSql, new { DossierId = dossierId.ToString() }, transaction);

            if (!string.IsNullOrEmpty(stepId) && !string.IsNullOrWhiteSpace(assignees))
            {
                var insertSql = @"
                    INSERT INTO WORKFLOW_TASKS_ACTIVE (
                        ID, DOSSIER_ID, CURRENT_STEP_ID, CURRENT_STEP_NAME, CURRENT_ASSIGNEES, AVAILABLE_ACTIONS, 
                        CREATED_BY, CREATED_DATE, LAST_MODIFIED_BY, LAST_MODIFIED_DATE
                    ) VALUES (
                        :Id, :DossierId, :CurrentStepId, :CurrentStepName, :CurrentAssignees, :AvailableActions, 
                        :CreatedBy, :CreatedDate, :LastModifiedBy, :LastModifiedDate
                    )";

                await _connection.ExecuteAsync(insertSql, new
                {
                    Id = Guid.NewGuid().ToString(),
                    DossierId = dossierId.ToString(),
                    CurrentStepId = stepId,
                    CurrentStepName = stepName,
                    CurrentAssignees = assignees,
                    AvailableActions = actionsJson,
                    CreatedBy = modifiedBy,
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedBy = modifiedBy,
                    LastModifiedDate = DateTime.UtcNow
                }, transaction);
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    public async Task<bool> UpdateFormDataAsync(Guid id, string formDataJson, int expectedRowVersion, string modifiedBy)
    {
        _connection.EnsureOpen();
        var sql = $@"UPDATE DOSSIERS SET
                        {nameof(Dossier.FormDataJson)} = :FormDataJson,
                        {nameof(Dossier.ModifiedBy)} = :ModifiedBy,
                        {nameof(Dossier.ModifiedDate)} = :ModifiedDate,
                        {nameof(Dossier.RowVersion)} = {nameof(Dossier.RowVersion)} + 1
                     WHERE {nameof(Dossier.Id)} = :Id
                       AND {nameof(Dossier.RowVersion)} = :ExpectedRowVersion
                       AND {nameof(Dossier.IsDeleted)} = 0";
        var affected = await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            FormDataJson = OracleClob.Param(formDataJson),
            ModifiedBy = modifiedBy,
            ModifiedDate = DateTime.UtcNow,
            ExpectedRowVersion = expectedRowVersion
        });
        if (affected == 0)
            throw new Exception("Concurrency conflict: Hồ sơ đã được cập nhật bởi người dùng khác.");
        return true;
    }
    public async Task<IEnumerable<DossierEquipmentDto>> GetEquipmentsAsync(Guid dossierId)
    {
        _connection.EnsureOpen();
        var sql = $@"SELECT
                        de.EquipmentId,
                        e.CODE as EquipmentCode,
                        e.NAME as EquipmentName,
                        e.SerialNumber,
                        et.NAME as EquipmentTypeName,
                        i.NAME as InfrastructureName
                     FROM DOSSIER_EQUIPMENTS de
                     INNER JOIN Equipments e ON de.EquipmentId = e.Id
                     LEFT JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id
                     LEFT JOIN INFRASTRUCTURE i ON e.Infrastructure_Id = i.ID
                     WHERE de.DossierId = :DossierId";
        return await _connection.QueryAsync<DossierEquipmentDto>(sql, new { DossierId = dossierId.ToString() });
    }
    public async Task<bool> AddEquipmentAsync(Guid dossierId, Guid equipmentId)
    {
        _connection.EnsureOpen();
        // Check không trùng
        var exists = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM DOSSIER_EQUIPMENTS WHERE DossierId = :DossierId AND EquipmentId = :EquipmentId",
            new { DossierId = dossierId.ToString(), EquipmentId = equipmentId.ToString() });
        if (exists > 0) return true;
        var affected = await _connection.ExecuteAsync(
            "INSERT INTO DOSSIER_EQUIPMENTS (DossierId, EquipmentId) VALUES (:DossierId, :EquipmentId)",
            new { DossierId = dossierId.ToString(), EquipmentId = equipmentId.ToString() });
        return affected > 0;
    }
    public async Task<bool> RemoveEquipmentAsync(Guid dossierId, Guid equipmentId)
    {
        _connection.EnsureOpen();
        var affected = await _connection.ExecuteAsync(
            "DELETE FROM DOSSIER_EQUIPMENTS WHERE DossierId = :DossierId AND EquipmentId = :EquipmentId",
            new { DossierId = dossierId.ToString(), EquipmentId = equipmentId.ToString() });
        return affected > 0;
    }
    public async Task<int> CreateVersionAsync(DossierVersion version)
    {
        _connection.EnsureOpen();
        // Lấy version number tiếp theo
        var maxVersion = await _connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(VersionNumber), 0) FROM DOSSIER_VERSIONS WHERE DossierId = :DossierId",
            new { DossierId = version.DossierId.ToString() });
        version.VersionNumber = maxVersion + 1;
        version.Id = Guid.Parse(UuidHelper.NewUuid());
        var sql = $@"INSERT INTO DOSSIER_VERSIONS (
                        {nameof(DossierVersion.Id)},
                        {nameof(DossierVersion.DossierId)},
                        {nameof(DossierVersion.VersionNumber)},
                        {nameof(DossierVersion.FormDataJson)},
                        {nameof(DossierVersion.DocumentsSnapshotJson)},
                        {nameof(DossierVersion.ChangeNote)},
                        {nameof(DossierVersion.CreatedBy)},
                        {nameof(DossierVersion.CreatedDate)}
                    ) VALUES (:Id, :DossierId, :VersionNumber, :FormDataJson, :DocumentsSnapshotJson, :ChangeNote, :CreatedBy, :CreatedDate)";
        await _connection.ExecuteAsync(sql, new
        {
            Id = version.Id.ToString(),
            DossierId = version.DossierId.ToString(),
            version.VersionNumber,
            FormDataJson = OracleClob.Param(version.FormDataJson),
            DocumentsSnapshotJson = OracleClob.Param(version.DocumentsSnapshotJson),
            version.ChangeNote,
            version.CreatedBy,
            version.CreatedDate
        });
        return version.VersionNumber;
    }
    public async Task<IEnumerable<DossierVersionDto>> GetVersionsAsync(Guid dossierId)
    {
        _connection.EnsureOpen();
        var sql = $@"SELECT
                        {nameof(DossierVersion.Id)},
                        {nameof(DossierVersion.DossierId)},
                        {nameof(DossierVersion.VersionNumber)},
                        {nameof(DossierVersion.FormDataJson)},
                        {nameof(DossierVersion.DocumentsSnapshotJson)},
                        {nameof(DossierVersion.ChangeNote)},
                        {nameof(DossierVersion.CreatedBy)},
                        {nameof(DossierVersion.CreatedDate)}
                     FROM DOSSIER_VERSIONS
                     WHERE {nameof(DossierVersion.DossierId)} = :DossierId
                     ORDER BY {nameof(DossierVersion.VersionNumber)} DESC";
        return await _connection.QueryAsync<DossierVersionDto>(sql, new { DossierId = dossierId.ToString() });
    }

    public async Task<DossierWorkflowStatusDto?> GetWorkflowStatusByEntityAsync(string entityId)
    {
        _connection.EnsureOpen();

        // Query 1: Get latest workflow instance and JOIN with definition name
        var sqlInstance = @"SELECT wi.ID, wi.WORKFLOWDEFINITIONID, wi.TARGETENTITYID, wi.ENTITYTYPE, 
                                   wi.STATUS, wi.CURRENTSTEPORDER, wi.CURRENTNODEID, wi.CURRENTNODENAME, 
                                   wi.CREATEDAT, wi.UPDATEDAT, wd.NAME as DefinitionName
                            FROM WORKFLOWINSTANCES wi
                            LEFT JOIN WORKFLOWDEFINITIONS wd ON wi.WORKFLOWDEFINITIONID = wd.ID
                            WHERE wi.TARGETENTITYID = :EntityId AND wi.ENTITYTYPE = 'Dossier'
                            ORDER BY wi.CREATEDAT DESC";
        
        var instance = await _connection.QueryFirstOrDefaultAsync<dynamic>(sqlInstance, new { EntityId = entityId });
        if (instance == null) return null;

        string instanceId = instance.ID.ToString();
        string workflowDefId = instance.WORKFLOWDEFINITIONID.ToString();
        string definitionName = instance.DEFINITIONNAME?.ToString() ?? string.Empty;

        // Query 2: Get all steps of this definition in one roundtrip
        var sqlSteps = @"SELECT Id, StepName, ""Order"", RequiredRole, ActionType, AllowEdit, RequireSignature 
                         FROM WORKFLOWSTEPS 
                         WHERE WorkflowDefinitionId = :Id 
                         ORDER BY ""Order""";
        var steps = (await _connection.QueryAsync<dynamic>(sqlSteps, new { Id = workflowDefId })).ToList();

        // Query 3: Get all tasks of this instance in one roundtrip
        var sqlTasks = @"SELECT Id, StepId, StepName, AssignedRole, AssigneeUserId, Status, CreatedAt 
                         FROM WORKFLOWTASKS 
                         WHERE WorkflowInstanceId = :InstanceId";
        var tasks = (await _connection.QueryAsync<dynamic>(sqlTasks, new { InstanceId = instanceId })).ToList();

        // Process steps & tasks in-memory to prevent N+1 queries
        var pendingTasks = tasks.Where(t => t.STATUS == "Pending").ToList();
        var firstPendingTask = pendingTasks.FirstOrDefault();
        
        dynamic currentStep = null;
        if (firstPendingTask != null)
        {
            string firstPendingStepId = firstPendingTask.STEPID?.ToString();
            currentStep = steps.FirstOrDefault(s => s.ID?.ToString() == firstPendingStepId);
        }
        else
        {
            int currentStepOrder = Convert.ToInt32(instance.CURRENTSTEPORDER);
            currentStep = steps.FirstOrDefault(s => Convert.ToInt32(s.Order) == currentStepOrder);
        }

        bool currentStepAllowEdit = instance.STATUS == "Running" && currentStep != null && Convert.ToInt32(currentStep.AllowEdit) == 1;

        var dto = new DossierWorkflowStatusDto
        {
            InstanceId = Guid.Parse(instanceId),
            WorkflowDefinitionId = Guid.Parse(workflowDefId),
            CurrentNodeId = instance.CURRENTNODEID?.ToString(),
            DefinitionName = definitionName,
            Status = instance.STATUS?.ToString() ?? string.Empty,
            CurrentStepOrder = Convert.ToInt32(instance.CURRENTSTEPORDER),
            CurrentStepName = instance.CURRENTNODENAME?.ToString() ?? currentStep?.STEPNAME?.ToString() ?? string.Empty,
            CurrentStepAllowEdit = currentStepAllowEdit,
            CreatedAt = Convert.ToDateTime(instance.CREATEDAT),
            UpdatedAt = Convert.ToDateTime(instance.UPDATEDAT)
        };

        foreach (var t in pendingTasks)
        {
            string tStepId = t.STEPID?.ToString();
            var stepOfTask = steps.FirstOrDefault(s => s.ID?.ToString() == tStepId);
            
            dto.PendingTasks.Add(new DossierWorkflowPendingTaskDto
            {
                Id = Guid.Parse(t.ID.ToString()),
                StepName = t.STEPNAME?.ToString() ?? string.Empty,
                AssignedRole = t.ASSIGNEDROLE?.ToString() ?? string.Empty,
                ActionType = stepOfTask?.ACTIONTYPE?.ToString() ?? string.Empty,
                AllowEdit = stepOfTask != null && Convert.ToInt32(stepOfTask.AllowEdit) == 1,
                CreatedAt = Convert.ToDateTime(t.CREATEDAT)
            });
        }

        return dto;
    }

    public async Task<EavFormTemplate?> GetEavFormTemplateAsync(Guid formId)
    {
        _connection.EnsureOpen();

        const string sql = @"
            SELECT 
                t.Id, v.Name as Name, t.Code as Code, v.Category as Category, v.Description as Description, v.DescriptionInfo as DescriptionInfo, t.ExtractionProcess,
                v.FormSchema as FormSchema, t.EquipmentTypeId, t.GridTypeId, v.Version as Version, t.IsActive as IsActive, t.CreatedAt,
                t.CreatedBy, t.Status, t.FormType, t.IsDeleted
            FROM EavFormTemplates t
            LEFT JOIN EavFormTemplateVersions v ON t.Id = v.FormTemplateId AND v.IsActive = 1 AND v.IsDeleted = 0 AND v.Version = (
                SELECT MAX(Version) FROM EavFormTemplateVersions WHERE FormTemplateId = t.Id AND IsActive = 1 AND IsDeleted = 0
            )
            WHERE t.Id = :FormId AND t.IsDeleted = 0";

        return await _connection.QuerySingleOrDefaultAsync<EavFormTemplate>(sql, new { FormId = formId.ToString() });
    }

    public async Task<EavFormTemplate?> GetEavFormTemplateByDossierIdAsync(Guid dossierId)
    {
        _connection.EnsureOpen();

        const string sql = @"
            SELECT 
                f.Id, v.Name as Name, f.Code as Code, v.Category as Category, v.Description as Description, v.DescriptionInfo as DescriptionInfo, f.ExtractionProcess,
                v.FormSchema as FormSchema, f.EquipmentTypeId, f.GridTypeId, v.Version as Version, f.IsActive as IsActive, f.CreatedAt,
                f.CreatedBy, f.Status, f.FormType, f.IsDeleted
            FROM DOSSIERS d
            INNER JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
            INNER JOIN EavFormTemplates f ON dt.FORM_ID = f.Id
            LEFT JOIN EavFormTemplateVersions v ON f.Id = v.FormTemplateId AND v.IsActive = 1 AND v.IsDeleted = 0 AND v.Version = (
                SELECT MAX(Version) FROM EavFormTemplateVersions WHERE FormTemplateId = f.Id AND IsActive = 1 AND IsDeleted = 0
            )
            WHERE d.Id = :DossierId AND d.IsDeleted = 0 AND dt.IsDeleted = 0 AND f.IsDeleted = 0";

        return await _connection.QuerySingleOrDefaultAsync<EavFormTemplate>(sql, new { DossierId = dossierId.ToString() });
    }

    public async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetDraftPagedFromDbAsync(DossierFilterDto filter, string userId)
    {
        _connection.EnsureOpen();
        var parameters = new DynamicParameters();
        
        var sqlBase = @" FROM DOSSIERS d
                         LEFT JOIN INFRASTRUCTURE i ON d.InfrastructureId = i.ID
                         LEFT JOIN DOSSIER_TYPES dt ON d.DossierTypeId = dt.ID
                         LEFT JOIN DOSSIER_SETS ds ON d.DossierSetId = ds.ID
                         LEFT JOIN DOSSIER_STATUSES dstat ON d.STATUS_ID = dstat.ID
                         WHERE d.IsDeleted = 0
                           AND d.STATUS_ID IN (1, 2)
                           AND d.CreatorId = :UserId";
        
        parameters.Add("UserId", userId);

        if (filter.InfrastructureId.HasValue)
        {
            sqlBase += " AND d.InfrastructureId = :InfrastructureId";
            parameters.Add("InfrastructureId", filter.InfrastructureId.Value.ToString());
        }

        if (filter.GridTypeId.HasValue)
        {
            sqlBase += " AND d.GridTypeId = :GridTypeId";
            parameters.Add("GridTypeId", filter.GridTypeId.Value);
        }

        if (filter.DossierTypeId.HasValue)
        {
            sqlBase += " AND d.DossierTypeId = :DossierTypeId";
            parameters.Add("DossierTypeId", filter.DossierTypeId.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = $"%{filter.Keyword.Trim().ToUpperInvariant()}%";
            sqlBase += " AND (UPPER(d.Id) LIKE :Keyword OR UPPER(i.NAME) LIKE :Keyword OR UPPER(i.CODE) LIKE :Keyword OR UPPER(dt.NAME) LIKE :Keyword)";
            parameters.Add("Keyword", keyword);
        }

        var countSql = $"SELECT COUNT(1) {sqlBase}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        if (totalCount == 0)
        {
            return (Enumerable.Empty<DossierListItemDto>(), 0);
        }

        var selectSql = $@"SELECT
                            d.Id,
                            d.GridTypeId,
                            d.InfrastructureId,
                            i.NAME as InfrastructureName,
                            i.CODE as InfrastructureCode,
                            d.DossierSetId,
                            ds.NAME as DossierSetName,
                            d.DossierTypeId,
                            dt.NAME as DossierTypeName,
                            d.STATUS_ID as StatusId,
                            dstat.CODE as StatusCode,
                            dstat.NAME as StatusName,
                            d.WorkflowStatusName,
                            d.CreatorId as CreatorId,
                            d.CreatorUsername as CreatorUsername,
                            d.CreatorName as CreatorName,
                            d.CreatedDate,
                            d.FormDataJson,
                            (SELECT COUNT(1) FROM DOCUMENTS doc WHERE doc.DOSSIER_ID = d.Id AND doc.IS_DELETED = 0) as DocumentCount
                         {sqlBase}
                         ORDER BY d.CreatedDate DESC
                         OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY";

        parameters.Add("Offset", (filter.Page - 1) * filter.PageSize);
        parameters.Add("PageSize", filter.PageSize);

        var rawItems = await _connection.QueryAsync<dynamic>(selectSql, parameters);
        var bhsCatalogs = await GetBhsCatalogDefinitionsAsync();

        var mappedItems = rawItems.Select(d => {
            var dto = new DossierListItemDto
            {
                Id = d.ID is string sId && Guid.TryParse(sId, out var gId) ? gId : (d.ID is Guid guidId ? guidId : Guid.Empty),
                GridTypeId = d.GRIDTYPEID == null ? (int?)null : Convert.ToInt32(d.GRIDTYPEID),
                InfrastructureId = d.INFRASTRUCTUREID is string sInfra && Guid.TryParse(sInfra, out var gInfra) ? gInfra : (d.INFRASTRUCTUREID is Guid guidInfra ? guidInfra : null),
                InfrastructureName = d.INFRASTRUCTURENAME,
                InfrastructureCode = d.INFRASTRUCTURECODE,
                DossierSetId = d.DOSSIERSETID is string sSet && Guid.TryParse(sSet, out var gSet) ? gSet : (d.DOSSIERSETID is Guid guidSet ? guidSet : null),
                DossierSetName = d.DOSSIERSETNAME,
                DossierTypeId = d.DOSSIERTYPEID is string sType && Guid.TryParse(sType, out var gType) ? gType : (d.DOSSIERTYPEID is Guid guidType ? guidType : Guid.Empty),
                DossierTypeName = d.DOSSIERTYPENAME,
                StatusId = d.STATUSID == null ? 0 : Convert.ToInt32(d.STATUSID),
                StatusCode = d.STATUSCODE,
                StatusName = d.STATUSNAME,
                WorkflowStatusName = d.WORKFLOWSTATUSNAME,
                CreatedDate = d.CREATEDDATE is DateTime dtVal ? dtVal : DateTime.MinValue,
                DocumentCount = d.DOCUMENTCOUNT == null ? 0 : Convert.ToInt32(d.DOCUMENTCOUNT),
                Creator = new CreatorInfoDto
                {
                    Id = d.CREATORID?.ToString() ?? string.Empty,
                    Username = d.CREATORUSERNAME?.ToString() ?? string.Empty,
                    Name = d.CREATORNAME?.ToString() ?? string.Empty
                }
            };

            string? formDataJson = d.FORMDATAJSON as string;
            dto.CatalogData = ParseCatalogData(formDataJson, bhsCatalogs);
            return dto;
        }).ToList();

        return (mappedItems, totalCount);
    }
}
