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

    public async Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = @"SELECT ID, CODE, NAME, INFRA_TYPE_ID as InfraTypeId, UNIT_ID as UnitId, GRIDTYPEID as GridTypeId, IS_ACTIVE as IsActive 
                    FROM INFRASTRUCTURE 
                    WHERE IsDeleted = 0 
                    ORDER BY NAME ASC";
        return await _connection.QueryAsync<InfrastructureEntity>(sql);
    }

    public async Task<IEnumerable<GridTypeEntity>> GetGridTypesLookupAsync()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = "SELECT Id, Name FROM GridTypes ORDER BY Id ASC";
        return await _connection.QueryAsync<GridTypeEntity>(sql);
    }
    public async Task<IEnumerable<DossierType>> GetDossierTypesLookupAsync()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

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
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var parameters = new DynamicParameters();
        var sqlBase = $@"FROM DOSSIERS d
                         LEFT JOIN INFRASTRUCTURE i ON d.{nameof(Dossier.InfrastructureId)} = i.ID
                         LEFT JOIN DOSSIER_TYPES dt ON d.{nameof(Dossier.DossierTypeId)} = dt.ID
                         LEFT JOIN DOSSIER_SETS ds ON d.{nameof(Dossier.DossierSetId)} = ds.ID
                         LEFT JOIN PUBLISH_STATUSES ps ON d.PUBLISHSTATUSID = ps.ID
                         WHERE d.{nameof(Dossier.IsDeleted)} = 0";

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
                            d.{nameof(Dossier.Status)},
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
                Status = d.STATUS ?? string.Empty,
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

    public async Task<DossierDetailDto?> GetDetailByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var sql = $@"SELECT
                        d.{nameof(Dossier.Id)},
                        d.{nameof(Dossier.GridTypeId)},
                        d.{nameof(Dossier.InfrastructureId)},
                        i.NAME as {nameof(DossierDetailDto.InfrastructureName)},
                        i.CODE as {nameof(DossierDetailDto.InfrastructureCode)},
                        d.{nameof(Dossier.DossierSetId)},
                        ds.NAME as {nameof(DossierDetailDto.DossierSetName)},
                        d.{nameof(Dossier.DossierTypeId)},
                        dt.NAME as {nameof(DossierDetailDto.DossierTypeName)},
                        dt.FORM_ID as {nameof(DossierDetailDto.FormId)},
                        d.{nameof(Dossier.FormDataJson)},
                        d.{nameof(Dossier.Status)},
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
                        d.{nameof(Dossier.CreatorId)} as Id,
                        d.{nameof(Dossier.CreatorUsername)} as Username,
                        d.{nameof(Dossier.CreatorName)} as Name
                     FROM DOSSIERS d
                     LEFT JOIN INFRASTRUCTURE i ON d.{nameof(Dossier.InfrastructureId)} = i.ID
                     LEFT JOIN DOSSIER_TYPES dt ON d.{nameof(Dossier.DossierTypeId)} = dt.ID
                     LEFT JOIN DOSSIER_SETS ds ON d.{nameof(Dossier.DossierSetId)} = ds.ID
                     LEFT JOIN PUBLISH_STATUSES ps ON d.PUBLISHSTATUSID = ps.ID
                     WHERE d.{nameof(Dossier.Id)} = :Id AND d.{nameof(Dossier.IsDeleted)} = 0";
        var dossierList = await _connection.QueryAsync<DossierDetailDto, CreatorInfoDto, DossierDetailDto>(
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

        // Get equipment list
        dossier.Equipments = (await GetEquipmentsAsync(id)).ToList();
        return dossier;
    }
    public async Task<Dossier?> GetByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var sql = $@"SELECT * FROM DOSSIERS WHERE {nameof(Dossier.Id)} = :Id AND {nameof(Dossier.IsDeleted)} = 0";
        return await _connection.QuerySingleOrDefaultAsync<Dossier>(sql, new { Id = id.ToString() });
    }
    public async Task<Guid> CreateAsync(Dossier dossier, IEnumerable<Guid> equipmentIds)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        if (dossier.Id == Guid.Empty)
            dossier.Id = Guid.Parse(UuidHelper.NewUuid());
        using var transaction = _connection.BeginTransaction();
        try
        {
            var sql = $@"INSERT INTO DOSSIERS (
                            {nameof(Dossier.Id)},
                            {nameof(Dossier.GridTypeId)},
                            {nameof(Dossier.InfrastructureId)},
                            {nameof(Dossier.DossierSetId)},
                            {nameof(Dossier.DossierTypeId)},
                            {nameof(Dossier.FormDataJson)},
                            {nameof(Dossier.Status)},
                            {nameof(Dossier.RowVersion)},
                            {nameof(Dossier.CreatorId)},
                            {nameof(Dossier.CreatorUsername)},
                            {nameof(Dossier.CreatorName)},
                            {nameof(Dossier.CreatedBy)},
                            {nameof(Dossier.CreatedDate)},
                            {nameof(Dossier.IsDeleted)}
                        ) VALUES (
                            :Id, :GridTypeId, :InfrastructureId, :DossierSetId, :DossierTypeId,
                            :FormDataJson, :Status, :RowVersion, :CreatorId, :CreatorUsername,
                            :CreatorName, :CreatedBy, :CreatedDate, :IsDeleted
                        )";
            await _connection.ExecuteAsync(sql, new
            {
                Id = dossier.Id.ToString(),
                dossier.GridTypeId,
                InfrastructureId = dossier.InfrastructureId?.ToString(),
                DossierSetId = dossier.DossierSetId?.ToString(),
                DossierTypeId = dossier.DossierTypeId.ToString(),
                dossier.FormDataJson,
                dossier.Status,
                dossier.RowVersion,
                CreatorId = dossier.CreatorId?.ToString(),
                dossier.CreatorUsername,
                dossier.CreatorName,
                dossier.CreatedBy,
                dossier.CreatedDate,
                IsDeleted = dossier.IsDeleted ? 1 : 0
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
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var sql = $@"UPDATE DOSSIERS SET
                            {nameof(Dossier.GridTypeId)} = :GridTypeId,
                            {nameof(Dossier.InfrastructureId)} = :InfrastructureId,
                            {nameof(Dossier.DossierSetId)} = :DossierSetId,
                            {nameof(Dossier.DossierTypeId)} = :DossierTypeId,
                            {nameof(Dossier.FormDataJson)} = :FormDataJson,
                            {nameof(Dossier.ModifiedBy)} = :ModifiedBy,
                            {nameof(Dossier.ModifiedDate)} = :ModifiedDate,
                            {nameof(Dossier.RowVersion)} = {nameof(Dossier.RowVersion)} + 1
                         WHERE {nameof(Dossier.Id)} = :Id
                           AND {nameof(Dossier.RowVersion)} = :RowVersion
                           AND {nameof(Dossier.IsDeleted)} = 0";
            var affected = await _connection.ExecuteAsync(sql, new
            {
                Id = dossier.Id.ToString(),
                dossier.GridTypeId,
                InfrastructureId = dossier.InfrastructureId?.ToString(),
                DossierSetId = dossier.DossierSetId?.ToString(),
                DossierTypeId = dossier.DossierTypeId.ToString(),
                dossier.FormDataJson,
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
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
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
    public async Task<bool> UpdateStatusAsync(Guid id, string status, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var sql = $@"UPDATE DOSSIERS SET
                        {nameof(Dossier.Status)} = :Status,
                        {nameof(Dossier.ModifiedBy)} = :ModifiedBy,
                        {nameof(Dossier.ModifiedDate)} = :ModifiedDate
                     WHERE {nameof(Dossier.Id)} = :Id AND {nameof(Dossier.IsDeleted)} = 0";
        var affected = await _connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            Status = status,
            ModifiedBy = modifiedBy,
            ModifiedDate = DateTime.UtcNow
        });
        return affected > 0;
    }
    public async Task<bool> UpdateWorkflowAsync(Guid id, Guid workflowInstanceId, string workflowStatusName, string status, int? publishStatusId, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var sql = $@"UPDATE DOSSIERS SET
                        {nameof(Dossier.WorkflowInstanceId)} = :WorkflowInstanceId,
                        {nameof(Dossier.WorkflowStatusName)} = :WorkflowStatusName,
                        {nameof(Dossier.Status)} = :Status,
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
            Status = status,
            PublishStatusId = publishStatusId,
            ModifiedBy = modifiedBy,
            ModifiedDate = DateTime.UtcNow
        });
        return affected > 0;
    }

    public async Task<bool> UpdatePublishStatusAsync(Guid id, int publishStatusId, string modifiedBy)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
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
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

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
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
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
            FormDataJson = formDataJson,
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
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
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
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
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
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        var affected = await _connection.ExecuteAsync(
            "DELETE FROM DOSSIER_EQUIPMENTS WHERE DossierId = :DossierId AND EquipmentId = :EquipmentId",
            new { DossierId = dossierId.ToString(), EquipmentId = equipmentId.ToString() });
        return affected > 0;
    }
    public async Task<int> CreateVersionAsync(DossierVersion version)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
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
            version.FormDataJson,
            version.DocumentsSnapshotJson,
            version.ChangeNote,
            version.CreatedBy,
            version.CreatedDate
        });
        return version.VersionNumber;
    }
    public async Task<IEnumerable<DossierVersionDto>> GetVersionsAsync(Guid dossierId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
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
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

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
}