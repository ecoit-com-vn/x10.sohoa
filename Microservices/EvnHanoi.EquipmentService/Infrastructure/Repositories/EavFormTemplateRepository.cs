using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class EavFormTemplateRepository : IEavFormTemplateRepository
{
    private readonly IDbConnection _connection;

    public EavFormTemplateRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<EavFormTemplate?> GetByIdAsync(Guid id)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"SELECT t.*, v.Code as Code, v.Name as Name, v.Category as Category, v.Description as Description, v.DescriptionInfo as DescriptionInfo,
                            v.FormSchema as FormSchema, v.Version as Version,
                            gt.Name as {nameof(EavFormTemplate.GridTypeName)},
                            et.Name as {nameof(EavFormTemplate.EquipmentTypeName)},
                            cat.Name as {nameof(EavFormTemplate.CategoryName)}
                     FROM {nameof(EavFormTemplate)}s t
                     LEFT JOIN EavFormTemplateVersions v ON t.{nameof(EavFormTemplate.Id)} = v.FormTemplateId AND v.IsActive = 1 AND v.IsDeleted = 0 AND v.Version = (
                         SELECT MAX(Version) FROM EavFormTemplateVersions WHERE FormTemplateId = t.{nameof(EavFormTemplate.Id)} AND IsActive = 1 AND IsDeleted = 0
                     )
                     LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                     LEFT JOIN EquipmentTypes et ON t.{nameof(EavFormTemplate.EquipmentTypeId)} = et.Id
                     LEFT JOIN CATALOG_TYPE hmad ON hmad.Code = 'HMAD' AND hmad.IsDeleted = 0
                     LEFT JOIN {nameof(Catalog)} cat ON cat.CatalogTypeId = hmad.Id AND cat.IsDeleted = 0
                          AND (cat.Code = v.Category OR TO_CHAR(cat.Id) = v.Category)
                     WHERE t.{nameof(EavFormTemplate.Id)}= :Id";
        return await _connection.QuerySingleOrDefaultAsync<EavFormTemplate>(sql, new { Id = id.ToString() });
    }

    /// <summary>
    /// Biểu mẫu thông số thiết bị: FormType = TEMPLATE gắn EquipmentType,
    /// lấy FormSchema JSON từ EavFormTemplateVersions active mới nhất.
    /// </summary>
    public async Task<EavFormTemplate?> GetActiveByEquipmentTypeIdAsync(Guid equipmentTypeId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"SELECT * FROM (
                         SELECT t.Id,
                                v.Code AS Code,
                                v.Name AS Name,
                                v.Category AS Category,
                                v.Description AS Description,
                                v.DescriptionInfo AS DescriptionInfo,
                                t.ExtractionProcess,
                                v.FormSchema AS FormSchema,
                                t.EquipmentTypeId,
                                t.GridTypeId,
                                v.Version AS Version,
                                t.IsActive AS IsActive,
                                t.CreatedAt,
                                t.CreatedBy,
                                t.Status,
                                t.FormType,
                                t.IsDeleted,
                                gt.Name AS {nameof(EavFormTemplate.GridTypeName)},
                                et.Name AS {nameof(EavFormTemplate.EquipmentTypeName)},
                                ROW_NUMBER() OVER (
                                    ORDER BY CASE WHEN v.Status = 'Hoàn thành' THEN 0 ELSE 1 END,
                                             v.Version DESC
                                ) AS rn
                         FROM {nameof(EavFormTemplate)}s t
                         INNER JOIN EavFormTemplateVersions v
                             ON t.Id = v.FormTemplateId
                            AND v.IsActive = 1
                            AND v.IsDeleted = 0
                         LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                         LEFT JOIN EquipmentTypes et ON t.{nameof(EavFormTemplate.EquipmentTypeId)} = et.Id
                         WHERE t.IsDeleted = 0
                           AND t.{nameof(EavFormTemplate.IsActive)} = 1
                           AND t.{nameof(EavFormTemplate.FormType)} = 'TEMPLATE'
                           AND t.{nameof(EavFormTemplate.EquipmentTypeId)} = :EquipmentTypeId
                     )
                     WHERE rn = 1";

        return await _connection.QuerySingleOrDefaultAsync<EavFormTemplate>(
            sql,
            new { EquipmentTypeId = equipmentTypeId.ToString() });
    }

    public async Task<IEnumerable<EavFormTemplate>> GetAllActiveAsync(string? formType = null, bool? isActive = true)
    {
        // Không lấy FormSchema — danh sách chỉ cần metadata; chi tiết JSON qua GetByIdAsync.
        var sql = $@"SELECT t.Id, v.Code as Code, v.Name as Name, v.Category as Category, v.Description as Description, v.DescriptionInfo as DescriptionInfo,
                            t.ExtractionProcess, t.EquipmentTypeId, t.GridTypeId, t.FormType,
                            v.Version as Version, t.IsActive as IsActive, t.CreatedAt,
                            t.CreatedBy as CreatedBy,
                            COALESCE(creatorById.FullName, creatorByUserName.FullName, t.CreatedBy) as CreatorFullName,
                            t.Status, t.IsDeleted,
                            gt.Name as {nameof(EavFormTemplate.GridTypeName)}, et.Name as {nameof(EavFormTemplate.EquipmentTypeName)}
                     FROM {nameof(EavFormTemplate)}s t
                     LEFT JOIN EavFormTemplateVersions v ON t.{nameof(EavFormTemplate.Id)} = v.FormTemplateId AND v.IsActive = 1 AND v.IsDeleted = 0 AND v.Version = (
                         SELECT MAX(Version) FROM EavFormTemplateVersions WHERE FormTemplateId = t.{nameof(EavFormTemplate.Id)} AND IsActive = 1 AND IsDeleted = 0
                     )
                     LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                     LEFT JOIN EquipmentTypes et ON t.{nameof(EavFormTemplate.EquipmentTypeId)} = et.Id
                     LEFT JOIN APP_USER creatorById ON creatorById.Id = t.CreatedBy AND creatorById.IsDeleted = 0
                     LEFT JOIN APP_USER creatorByUserName
                          ON UPPER(TRIM(creatorByUserName.UserName)) = UPPER(TRIM(t.CreatedBy))
                         AND creatorByUserName.IsDeleted = 0
                     WHERE t.IsDeleted = 0";
        if (isActive.HasValue)
        {
            sql += $" AND t.{nameof(EavFormTemplate.IsActive)} = :IsActive";
        }
        if (!string.IsNullOrEmpty(formType))
        {
            sql += $" AND t.{nameof(EavFormTemplate.FormType)} = :FormType";
        }
        sql += $" ORDER BY t.{nameof(EavFormTemplate.CreatedAt)} DESC";
        return await _connection.QueryAsync<EavFormTemplate>(sql, new { FormType = formType, IsActive = isActive.HasValue && isActive.Value ? 1 : 0 });
    }

    public async Task<(IEnumerable<EavFormTemplate> Items, int TotalCount)> GetDesignFormsAsync(EavFormTemplateFilterDto filterDto)
    {
        return await GetFormsByConditionAsync(filterDto);
    }

    public async Task<IEnumerable<EavFormTemplate>> GetApprovalFormsAsync()
    {
        return await GetFormsByScopeAsync(new[] { "Chờ duyệt", "Hoàn thành", "Từ chối" });
    }

    public async Task<IEnumerable<EavFormTemplate>> GetCompletedFormsAsync()
    {
        return await GetFormsByScopeAsync(new[] { "Hoàn thành" });
    }

    public async Task<IEnumerable<EavFormTemplate>> GetCompletedActiveFormsAsync()
    {
        return await GetFormsByScopeAsync(new[] { "Hoàn thành" }, isActive: true);
    }

    private async Task<IEnumerable<EavFormTemplate>> GetFormsByScopeAsync(string[]? statuses, bool? isActive = null)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        // Không lấy FormSchema — danh sách chỉ metadata; chi tiết JSON qua GetByIdAsync / GetByIdAndVersionAsync.
        // CategoryName: LEFT JOIN Catalog HMAD (1 query, không N+1).
        var sql = $@"SELECT t.Id, v.Code as Code, v.Name as Name, v.Category as Category, v.Description as Description, v.DescriptionInfo as DescriptionInfo,
                            t.ExtractionProcess, t.EquipmentTypeId, t.GridTypeId, t.FormType,
                            v.Version as Version, t.IsActive as IsActive, t.CreatedAt,
                            t.CreatedBy as CreatedBy,
                            COALESCE(creatorById.FullName, creatorByUserName.FullName, t.CreatedBy) as CreatorFullName,
                            t.Status, t.IsDeleted,
                            gt.Name as {nameof(EavFormTemplate.GridTypeName)},
                            et.Name as {nameof(EavFormTemplate.EquipmentTypeName)},
                            cat.Name as {nameof(EavFormTemplate.CategoryName)}
                     FROM {nameof(EavFormTemplate)}s t
                     LEFT JOIN EavFormTemplateVersions v ON t.{nameof(EavFormTemplate.Id)} = v.FormTemplateId AND v.IsActive = 1 AND v.IsDeleted = 0 AND v.Version = (
                         SELECT MAX(Version) FROM EavFormTemplateVersions WHERE FormTemplateId = t.{nameof(EavFormTemplate.Id)} AND IsActive = 1 AND IsDeleted = 0
                     )
                     LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                     LEFT JOIN EquipmentTypes et ON t.{nameof(EavFormTemplate.EquipmentTypeId)} = et.Id
                     LEFT JOIN CATALOG_TYPE hmad ON hmad.Code = 'HMAD' AND hmad.IsDeleted = 0
                     LEFT JOIN APP_USER creatorById ON creatorById.Id = t.CreatedBy AND creatorById.IsDeleted = 0
                     LEFT JOIN APP_USER creatorByUserName
                          ON UPPER(TRIM(creatorByUserName.UserName)) = UPPER(TRIM(t.CreatedBy))
                         AND creatorByUserName.IsDeleted = 0
                     LEFT JOIN {nameof(Catalog)} cat ON cat.CatalogTypeId = hmad.Id AND cat.IsDeleted = 0
                          AND (cat.Code = v.Category OR TO_CHAR(cat.Id) = v.Category)
                     WHERE t.IsDeleted = 0
                       AND t.{nameof(EavFormTemplate.FormType)} = 'FORM'";

        if (statuses is { Length: > 0 })
        {
            sql += $" AND t.{nameof(EavFormTemplate.Status)} IN :Statuses";
        }

        if (isActive.HasValue)
        {
            sql += $" AND t.{nameof(EavFormTemplate.IsActive)} = :IsActive";
        }

        sql += $" ORDER BY t.{nameof(EavFormTemplate.CreatedAt)} DESC";
        return await _connection.QueryAsync<EavFormTemplate>(sql, new
        {
            Statuses = statuses,
            IsActive = isActive.HasValue && isActive.Value ? 1 : 0
        });
    }

    private async Task<(IEnumerable<EavFormTemplate> Items, int TotalCount)> GetFormsByConditionAsync(EavFormTemplateFilterDto filterDto)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var whereClause = @"
        WHERE t.IsDeleted = 0
          AND t.FormType = 'FORM'";

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filterDto.Keyword))
        {
            whereClause += @"
            AND (
                LOWER(v.Code) LIKE :Keyword
                OR LOWER(v.Name) LIKE :Keyword
            )";

            parameters.Add("Keyword", $"%{filterDto.Keyword.Trim().ToLower()}%");
        }

        if (!string.IsNullOrWhiteSpace(filterDto.Status))
        {
            whereClause += " AND t.Status = :Status";
            parameters.Add("Status", filterDto.Status);
        }

        if (filterDto.StartDate.HasValue)
        {
            whereClause += " AND CREATED_DATE >= :StartDate";
            parameters.Add("StartDate", filterDto.StartDate.Value);
        }

        if (filterDto.EndDate.HasValue)
        {
            whereClause += " AND CREATED_DATE <= :EndDate";
            parameters.Add("EndDate", filterDto.EndDate.Value);
        }

        var countSql = $@"
        SELECT COUNT(*)
        FROM EavFormTemplates t
        LEFT JOIN EavFormTemplateVersions v
            ON t.Id = v.FormTemplateId
           AND v.IsActive = 1
           AND v.IsDeleted = 0
           AND v.Version = (
                SELECT MAX(Version)
                FROM EavFormTemplateVersions
                WHERE FormTemplateId = t.Id
                  AND IsActive = 1
                  AND IsDeleted = 0
           )
        {whereClause}";

        var sql = $@"
        SELECT
            t.Id,
            v.Code,
            v.Name,
            v.Category,
            v.Description,
            v.DescriptionInfo,
            t.ExtractionProcess,
            t.EquipmentTypeId,
            t.GridTypeId,
            t.FormType,
            v.Version,
            t.IsActive,
            t.CreatedAt,
            us.FullName AS CreatedBy,
            t.Status,
            t.IsDeleted,
            gt.Name AS {nameof(EavFormTemplate.GridTypeName)},
            et.Name AS {nameof(EavFormTemplate.EquipmentTypeName)},
            cat.Name AS {nameof(EavFormTemplate.CategoryName)}
        FROM EavFormTemplates t
        LEFT JOIN EavFormTemplateVersions v
            ON t.Id = v.FormTemplateId
           AND v.IsActive = 1
           AND v.IsDeleted = 0
           AND v.Version = (
                SELECT MAX(Version)
                FROM EavFormTemplateVersions
                WHERE FormTemplateId = t.Id
                  AND IsActive = 1
                  AND IsDeleted = 0
           )
        LEFT JOIN GridTypes gt
            ON t.GridTypeId = gt.Id
        LEFT JOIN EquipmentTypes et
            ON t.EquipmentTypeId = et.Id
        LEFT JOIN CATALOG_TYPE hmad
            ON hmad.Code = 'HMAD'
           AND hmad.IsDeleted = 0
        LEFT JOIN APP_USER us
            ON us.UserName = t.CreatedBy
        LEFT JOIN Catalog cat
            ON cat.CatalogTypeId = hmad.Id
           AND cat.IsDeleted = 0
           AND (cat.Code = v.Category OR TO_CHAR(cat.Id) = v.Category)

        {whereClause}

        ORDER BY t.CreatedAt DESC"; 

        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        var items = await _connection.QueryAsync<EavFormTemplate>(sql, parameters);

        return (items, totalCount);
    }

    public async Task AddAsync(EavFormTemplate template)
    {
        var sql = $@"INSERT INTO {nameof(EavFormTemplate)}s (
                    {nameof(EavFormTemplate.Id)}, 
                    {nameof(EavFormTemplate.Name)}, 
                    {nameof(EavFormTemplate.Code)}, 
                    {nameof(EavFormTemplate.Category)}, 
                    {nameof(EavFormTemplate.Description)}, 
                    {nameof(EavFormTemplate.DescriptionInfo)}, 
                    {nameof(EavFormTemplate.ExtractionProcess)}, 
                    {nameof(EavFormTemplate.ExtractionPosition)},
                    {nameof(EavFormTemplate.EquipmentTypeId)},
                    {nameof(EavFormTemplate.GridTypeId)},
                    {nameof(EavFormTemplate.IsActive)}, 
                    {nameof(EavFormTemplate.CreatedAt)}, 
                    {nameof(EavFormTemplate.CreatedBy)},
                    {nameof(EavFormTemplate.Status)},
                    {nameof(EavFormTemplate.FormType)},
                    IsDeleted
                )
                VALUES (:Id, :Name, :Code, :Category, :Description, :DescriptionInfo, :ExtractionProcess, :ExtractionPosition, :EquipmentTypeId, :GridTypeId, :IsActive, :CreatedAt, :CreatedBy, :Status, :FormType, :IsDeleted)";

        var param = BuildWriteParameters(template, includeId: true, includeAudit: true);
        await _connection.ExecuteAsync(sql, param);
    }

    public async Task UpdateAsync(EavFormTemplate template)
    {
        var sql = $@"UPDATE {nameof(EavFormTemplate)}s
                     SET {nameof(EavFormTemplate.Name)} = :Name,
                         {nameof(EavFormTemplate.Code)} = :Code,
                         {nameof(EavFormTemplate.Category)} = :Category,
                         {nameof(EavFormTemplate.Description)} = :Description,
                         {nameof(EavFormTemplate.DescriptionInfo)} = :DescriptionInfo,
                         {nameof(EavFormTemplate.ExtractionProcess)} = :ExtractionProcess,
                         {nameof(EavFormTemplate.ExtractionPosition)} = :ExtractionPosition,
                         {nameof(EavFormTemplate.EquipmentTypeId)} = :EquipmentTypeId,
                         {nameof(EavFormTemplate.GridTypeId)} = :GridTypeId,
                         {nameof(EavFormTemplate.IsActive)} = :IsActive,
                         {nameof(EavFormTemplate.Status)} = :Status,
                         {nameof(EavFormTemplate.FormType)} = :FormType,
                         IsDeleted = :IsDeleted
                     WHERE {nameof(EavFormTemplate.Id)} = :Id";
        
        var param = BuildWriteParameters(template, includeId: true, includeAudit: false);
        await _connection.ExecuteAsync(sql, param);
    }

    private static DynamicParameters BuildWriteParameters(EavFormTemplate template, bool includeId, bool includeAudit)
    {
        var parameters = new DynamicParameters();
        if (includeId)
            parameters.Add("Id", template.Id.ToString());

        parameters.Add("Name", template.Name);
        parameters.Add("Code", template.Code);
        parameters.Add("Category", template.Category);
        parameters.Add("Description", template.Description);
        parameters.Add("DescriptionInfo", template.DescriptionInfo);
        parameters.Add("ExtractionProcess", template.ExtractionProcess);
        parameters.Add("ExtractionPosition", template.ExtractionPosition);
        parameters.Add("EquipmentTypeId", template.EquipmentTypeId?.ToString());
        parameters.Add("GridTypeId", template.GridTypeId);
        parameters.Add("IsActive", template.IsActive ? 1 : 0);
        if (includeAudit)
        {
            parameters.Add("CreatedAt", template.CreatedAt);
            parameters.Add("CreatedBy", template.CreatedBy);
        }
        parameters.Add("Status", template.Status);
        parameters.Add("FormType", template.FormType);
        parameters.Add("IsDeleted", template.IsDeleted ? 1 : 0);
        return parameters;
    }

    public async Task<IEnumerable<EavFormTemplate>> GetVersionsByCodeAsync(string code)
    {
        // Không lấy FormSchema — danh sách lịch sử chỉ cần metadata; chi tiết JSON qua GetByIdAndVersionAsync.
        var sql = $@"SELECT t.Id,
                            v.Code as Code, 
                            v.Name as Name, 
                            v.Category as Category, 
                            v.Description as Description, 
                            v.DescriptionInfo as DescriptionInfo, 
                            v.ExtractionPosition as ExtractionPosition,
                            t.ExtractionProcess,
                            t.EquipmentTypeId, t.GridTypeId, t.FormType,
                            v.Version as Version, v.IsActive as IsActive,
                            v.CreatedAt as CreatedAt, v.CreatedBy as CreatedBy, v.Status as Status, v.IsDeleted as IsDeleted,
                            gt.Name as GridTypeName, et.Name as {nameof(EavFormTemplate.EquipmentTypeName)}, u.FullName as CreatorFullName
                     FROM {nameof(EavFormTemplate)}s t
                     INNER JOIN EavFormTemplateVersions v ON t.Id = v.FormTemplateId AND v.IsDeleted = 0
                     LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                     LEFT JOIN EquipmentTypes et ON t.{nameof(EavFormTemplate.EquipmentTypeId)} = et.Id
                     LEFT JOIN APP_USER u ON (v.CreatedBy = u.Id OR v.CreatedBy = u.UserName)
                     WHERE t.{nameof(EavFormTemplate.Code)} = :Code AND t.IsDeleted = 0
                     ORDER BY v.Version DESC";

        return await _connection.QueryAsync<EavFormTemplate>(sql, new { Code = code });
    }

    public async Task<EavFormTemplate?> GetByIdAndVersionAsync(Guid id, int version)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"SELECT t.Id,
                            v.Code as Code,
                            v.Name as Name,
                            v.Category as Category,
                            v.Description as Description,
                            v.DescriptionInfo as DescriptionInfo,
                            t.ExtractionProcess,
                            t.EquipmentTypeId, t.GridTypeId, t.FormType,
                            v.FormSchema as FormSchema, v.Version as Version, v.IsActive as IsActive,
                            v.CreatedAt as CreatedAt, v.CreatedBy as CreatedBy, v.Status as Status, v.IsDeleted as IsDeleted,
                            gt.Name as GridTypeName, et.Name as {nameof(EavFormTemplate.EquipmentTypeName)},
                            cat.Name as {nameof(EavFormTemplate.CategoryName)},
                            u.FullName as CreatorFullName
                     FROM {nameof(EavFormTemplate)}s t
                     INNER JOIN EavFormTemplateVersions v ON t.Id = v.FormTemplateId AND v.IsDeleted = 0 AND v.Version = :Version
                     LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                     LEFT JOIN EquipmentTypes et ON t.{nameof(EavFormTemplate.EquipmentTypeId)} = et.Id
                     LEFT JOIN CATALOG_TYPE hmad ON hmad.Code = 'HMAD' AND hmad.IsDeleted = 0
                     LEFT JOIN {nameof(Catalog)} cat ON cat.CatalogTypeId = hmad.Id AND cat.IsDeleted = 0
                          AND (cat.Code = v.Category OR TO_CHAR(cat.Id) = v.Category)
                     LEFT JOIN APP_USER u ON (v.CreatedBy = u.Id OR v.CreatedBy = u.UserName)
                     WHERE t.{nameof(EavFormTemplate.Id)} = :Id AND t.IsDeleted = 0";

        return await _connection.QuerySingleOrDefaultAsync<EavFormTemplate>(
            sql,
            new { Id = id.ToString(), Version = version });
    }

    // Version management methods
    public async Task AddVersionAsync(EavFormTemplateVersion version)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = @"INSERT INTO EavFormTemplateVersions (Id, FormTemplateId, Code, Name, Category, Description, DescriptionInfo, ExtractionPosition, FormSchema, Version, IsActive, CreatedAt, CreatedBy, Status, IsDeleted)
                    VALUES (:Id, :FormTemplateId, :Code, :Name, :Category, :Description, :DescriptionInfo, :ExtractionPosition, :FormSchema, :Version, :IsActive, :CreatedAt, :CreatedBy, :Status, :IsDeleted)";
        var param = new DynamicParameters();
        param.Add("Id", version.Id.ToString());
        param.Add("FormTemplateId", version.FormTemplateId.ToString());
        param.Add("Code", version.Code);
        param.Add("Name", version.Name);
        param.Add("Category", version.Category);
        param.Add("Description", version.Description);
        param.Add("DescriptionInfo", version.DescriptionInfo);
        param.Add("ExtractionPosition", version.ExtractionPosition);
        param.Add("FormSchema", OracleClob.Param(version.FormSchema));
        param.Add("Version", version.Version);
        param.Add("IsActive", version.IsActive ? 1 : 0);
        param.Add("CreatedAt", version.CreatedAt);
        param.Add("CreatedBy", version.CreatedBy);
        param.Add("Status", version.Status);
        param.Add("IsDeleted", version.IsDeleted ? 1 : 0);
        await _connection.ExecuteAsync(sql, param);
    }

    public async Task DeactivateVersionsAsync(Guid formTemplateId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = @"UPDATE EavFormTemplateVersions SET IsActive = 0 WHERE FormTemplateId = :FormTemplateId AND IsDeleted = 0";
        await _connection.ExecuteAsync(sql, new { FormTemplateId = formTemplateId.ToString() });
    }

    public async Task<int> GetMaxVersionAsync(Guid formTemplateId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = @"SELECT MAX(Version) FROM EavFormTemplateVersions WHERE FormTemplateId = :FormTemplateId AND IsDeleted = 0";
        var val = await _connection.QuerySingleOrDefaultAsync<int?>(sql, new { FormTemplateId = formTemplateId.ToString() });
        return val ?? 0;
    }

    public async Task DeleteVersionsAsync(Guid formTemplateId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = @"UPDATE EavFormTemplateVersions SET IsDeleted = 1 WHERE FormTemplateId = :FormTemplateId";
        await _connection.ExecuteAsync(sql, new { FormTemplateId = formTemplateId.ToString() });
    }

    public async Task ApproveVersionAsync(Guid formTemplateId, string status)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sqlGet = @"SELECT Id FROM EavFormTemplateVersions 
                       WHERE FormTemplateId = :FormTemplateId AND IsDeleted = 0 
                       ORDER BY Version DESC 
                       FETCH FIRST 1 ROWS ONLY";
        var latestVerId = await _connection.QueryFirstOrDefaultAsync<string>(sqlGet, new { FormTemplateId = formTemplateId.ToString() });
        if (!string.IsNullOrEmpty(latestVerId))
        {
            if (status == "Hoàn thành")
            {
                var sqlDeact = @"UPDATE EavFormTemplateVersions SET IsActive = 0 WHERE FormTemplateId = :FormTemplateId AND Id != :LatestVerId";
                await _connection.ExecuteAsync(sqlDeact, new { FormTemplateId = formTemplateId.ToString(), LatestVerId = latestVerId });

                var sqlAct = @"UPDATE EavFormTemplateVersions SET IsActive = 1, Status = 'Hoàn thành' WHERE Id = :LatestVerId";
                await _connection.ExecuteAsync(sqlAct, new { LatestVerId = latestVerId });
            }
            else
            {
                var sqlReject = @"UPDATE EavFormTemplateVersions SET Status = :Status WHERE Id = :LatestVerId";
                await _connection.ExecuteAsync(sqlReject, new { LatestVerId = latestVerId, Status = status });
            }
        }
    }

    public async Task ActivateVersionAsync(Guid versionId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var sqlGet = "SELECT FormTemplateId, Code, Name, Category, Description, DescriptionInfo FROM EavFormTemplateVersions WHERE Id = :Id AND IsDeleted = 0";
        var version = await _connection.QuerySingleOrDefaultAsync<dynamic>(sqlGet, new { Id = versionId.ToString() });

        if (version == null)
        {
            throw new Exception("Không tìm thấy phiên bản mẫu biểu này.");
        }

        string formTemplateId = version.FormTemplateId;

        var sqlDeact = "UPDATE EavFormTemplateVersions SET IsActive = 0 WHERE FormTemplateId = :FormTemplateId AND IsDeleted = 0";
        await _connection.ExecuteAsync(sqlDeact, new { FormTemplateId = formTemplateId });

        var sqlAct = "UPDATE EavFormTemplateVersions SET IsActive = 1 WHERE Id = :Id";
        await _connection.ExecuteAsync(sqlAct, new { Id = versionId.ToString() });

        // Sync metadata to parent table EavFormTemplates to prevent out-of-sync caching (e.g. in DossierTypes join)
        var sqlParent = $@"UPDATE {nameof(EavFormTemplate)}s
                          SET {nameof(EavFormTemplate.Name)} = COALESCE(:Name, {nameof(EavFormTemplate.Name)}),
                              {nameof(EavFormTemplate.Code)} = COALESCE(:Code, {nameof(EavFormTemplate.Code)}),
                              {nameof(EavFormTemplate.Category)} = :Category,
                              {nameof(EavFormTemplate.Description)} = :Description,
                              {nameof(EavFormTemplate.DescriptionInfo)} = :DescriptionInfo
                          WHERE {nameof(EavFormTemplate.Id)} = :Id";

        var targetName = Convert.ToString(version.Name);
        var targetCode = Convert.ToString(version.Code);

        await _connection.ExecuteAsync(sqlParent, new
        {
            Id = formTemplateId,
            Name = string.IsNullOrWhiteSpace(targetName) ? null : targetName,
            Code = string.IsNullOrWhiteSpace(targetCode) ? null : targetCode,
            Category = Convert.ToString(version.Category) ?? string.Empty,
            Description = Convert.ToString(version.Description) ?? string.Empty,
            DescriptionInfo = Convert.ToString(version.DescriptionInfo) ?? string.Empty
        });
    }

    public async Task<bool> RestoreVersionAsync(Guid formTemplateId, int version)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        // 1. Tự chữa lành dữ liệu nếu các cột Name, Code trong EavFormTemplateVersions bị NULL từ dữ liệu lịch sử
        var sqlHeal = @"UPDATE EavFormTemplateVersions v
                        SET v.Name = COALESCE(v.Name, (SELECT t.Name FROM EavFormTemplates t WHERE t.Id = v.FormTemplateId)),
                            v.Code = COALESCE(v.Code, (SELECT t.Code FROM EavFormTemplates t WHERE t.Id = v.FormTemplateId))
                        WHERE v.FormTemplateId = :FormTemplateId AND v.Version = :Version AND v.IsDeleted = 0
                          AND (v.Name IS NULL OR v.Code IS NULL)";
        await _connection.ExecuteAsync(sqlHeal, new { FormTemplateId = formTemplateId.ToString(), Version = version });

        // 2. Lấy đầy đủ thông tin của phiên bản cần khôi phục (bao gồm cả FormSchema)
        var sqlFind = @"SELECT Code, Name, Category, Description, DescriptionInfo, Version
                        FROM EavFormTemplateVersions
                        WHERE FormTemplateId = :FormTemplateId AND Version = :Version AND IsDeleted = 0";
        var target = await _connection.QueryFirstOrDefaultAsync<dynamic>(
            sqlFind,
            new { FormTemplateId = formTemplateId.ToString(), Version = version });
        if (target == null)
            return false;

        // 3. Đảm bảo chỉ 1 version hoạt động: ngưng tất cả → bật lại version chọn
        var sqlDeact = @"UPDATE EavFormTemplateVersions SET IsActive = 0 WHERE FormTemplateId = :FormTemplateId AND IsDeleted = 0";
        await _connection.ExecuteAsync(sqlDeact, new { FormTemplateId = formTemplateId.ToString() });

        var sqlAct = @"UPDATE EavFormTemplateVersions SET IsActive = 1, Status = 'Hoàn thành' WHERE FormTemplateId = :FormTemplateId AND Version = :Version AND IsDeleted = 0";
        await _connection.ExecuteAsync(sqlAct, new { FormTemplateId = formTemplateId.ToString(), Version = version });

        // 4. Cập nhật thông tin của biểu mẫu cha EavFormTemplates
        // Sử dụng COALESCE và truyền NULL thay vì chuỗi rỗng để tránh lỗi ORA-01407 khi Oracle chuyển chuỗi rỗng thành NULL
        var sqlParent = $@"UPDATE {nameof(EavFormTemplate)}s
                          SET {nameof(EavFormTemplate.Name)} = COALESCE(:Name, {nameof(EavFormTemplate.Name)}),
                              {nameof(EavFormTemplate.Code)} = COALESCE(:Code, {nameof(EavFormTemplate.Code)}),
                              {nameof(EavFormTemplate.Category)} = :Category,
                              {nameof(EavFormTemplate.Description)} = :Description,
                              {nameof(EavFormTemplate.DescriptionInfo)} = :DescriptionInfo,
                              IsActive = 1,
                              Status = 'Hoàn thành'
                          WHERE {nameof(EavFormTemplate.Id)} = :Id";

        var targetName = Convert.ToString(target.Name);
        var targetCode = Convert.ToString(target.Code);

        var paramParent = new DynamicParameters();
        paramParent.Add("Id", formTemplateId.ToString());
        paramParent.Add("Name", string.IsNullOrWhiteSpace(targetName) ? null : targetName);
        paramParent.Add("Code", string.IsNullOrWhiteSpace(targetCode) ? null : targetCode);
        paramParent.Add("Category", Convert.ToString(target.Category) ?? string.Empty);
        paramParent.Add("Description", Convert.ToString(target.Description) ?? string.Empty);
        paramParent.Add("DescriptionInfo", Convert.ToString(target.DescriptionInfo) ?? string.Empty);

        await _connection.ExecuteAsync(sqlParent, paramParent);

        return true;
    }
}
