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
                            v.FormSchema as FormSchema, v.Version as Version, gt.Name as {nameof(EavFormTemplate.GridTypeName)}, et.Name as {nameof(EavFormTemplate.EquipmentTypeName)} 
                     FROM {nameof(EavFormTemplate)}s t
                     LEFT JOIN EavFormTemplateVersions v ON t.{nameof(EavFormTemplate.Id)} = v.FormTemplateId AND v.IsDeleted = 0 AND v.Version = (
                         SELECT MAX(Version) FROM EavFormTemplateVersions WHERE FormTemplateId = t.{nameof(EavFormTemplate.Id)} AND IsDeleted = 0
                     )
                     LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                     LEFT JOIN EquipmentTypes et ON t.{nameof(EavFormTemplate.EquipmentTypeId)} = et.Id
                     WHERE et.Id = :Id AND t.IsDeleted = 0 AND t.IsActive = 1";
        return await _connection.QuerySingleOrDefaultAsync<EavFormTemplate>(sql, new { Id = id.ToString() });
    }

    public async Task<EavFormTemplate?> GetActiveByEquipmentTypeIdAsync(Guid equipmentTypeId)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var form = await QueryActiveTemplateByEquipmentTypeAsync(equipmentTypeId, "FORM");
        if (form != null)
            return form;

        return await QueryActiveTemplateByEquipmentTypeAsync(equipmentTypeId, "TEMPLATE");
    }

    private async Task<EavFormTemplate?> QueryActiveTemplateByEquipmentTypeAsync(Guid equipmentTypeId, string formType)
    {
        var sql = $@"SELECT * FROM (
                         SELECT t.*,
                                gt.Name AS {nameof(EavFormTemplate.GridTypeName)},
                                et.Name AS {nameof(EavFormTemplate.EquipmentTypeName)},
                                ROW_NUMBER() OVER (
                                    ORDER BY CASE WHEN t.{nameof(EavFormTemplate.Status)} = 'Hoàn thành' THEN 0 ELSE 1 END,
                                             t.{nameof(EavFormTemplate.Version)} DESC
                                ) AS rn
                         FROM {nameof(EavFormTemplate)}s t
                         LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                         LEFT JOIN EquipmentTypes et ON t.{nameof(EavFormTemplate.EquipmentTypeId)} = et.Id
                         WHERE t.IsDeleted = 0
                           AND t.{nameof(EavFormTemplate.IsActive)} = 1
                           AND t.{nameof(EavFormTemplate.FormType)} = :FormType
                           AND t.{nameof(EavFormTemplate.EquipmentTypeId)} = :EquipmentTypeId
                     )
                     WHERE rn = 1";

        return await _connection.QuerySingleOrDefaultAsync<EavFormTemplate>(
            sql,
            new { FormType = formType, EquipmentTypeId = equipmentTypeId.ToString() });
    }

    public async Task<IEnumerable<EavFormTemplate>> GetAllActiveAsync(string? formType = null, bool? isActive = true)
    {
        var sql = $@"SELECT t.*, v.Code as Code, v.Name as Name, v.Category as Category, v.Description as Description, v.DescriptionInfo as DescriptionInfo,
                            v.FormSchema as FormSchema, v.Version as Version, gt.Name as {nameof(EavFormTemplate.GridTypeName)}, et.Name as {nameof(EavFormTemplate.EquipmentTypeName)} 
                     FROM {nameof(EavFormTemplate)}s t
                     LEFT JOIN EavFormTemplateVersions v ON t.{nameof(EavFormTemplate.Id)} = v.FormTemplateId AND v.IsActive = 1 AND v.IsDeleted = 0 AND v.Version = (
                         SELECT MAX(Version) FROM EavFormTemplateVersions WHERE FormTemplateId = t.{nameof(EavFormTemplate.Id)} AND IsActive = 1 AND IsDeleted = 0
                     )
                     LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                     LEFT JOIN EquipmentTypes et ON t.{nameof(EavFormTemplate.EquipmentTypeId)} = et.Id
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

    public async Task<IEnumerable<EavFormTemplate>> GetDesignFormsAsync()
    {
        return await GetFormsByScopeAsync(null);
    }

    public async Task<IEnumerable<EavFormTemplate>> GetApprovalFormsAsync()
    {
        return await GetFormsByScopeAsync(new[] { "Chờ duyệt", "Hoàn thành", "Từ chối" });
    }

    public async Task<IEnumerable<EavFormTemplate>> GetCompletedFormsAsync()
    {
        return await GetFormsByScopeAsync(new[] { "Hoàn thành" });
    }

    private async Task<IEnumerable<EavFormTemplate>> GetFormsByScopeAsync(string[]? statuses)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var sql = $@"SELECT t.*, v.Code as Code, v.Name as Name, v.Category as Category, v.Description as Description, v.DescriptionInfo as DescriptionInfo,
                            v.FormSchema as FormSchema, v.Version as Version, gt.Name as {nameof(EavFormTemplate.GridTypeName)}, et.Name as {nameof(EavFormTemplate.EquipmentTypeName)}
                     FROM {nameof(EavFormTemplate)}s t
                     LEFT JOIN EavFormTemplateVersions v ON t.{nameof(EavFormTemplate.Id)} = v.FormTemplateId AND v.IsDeleted = 0 AND v.Version = (
                         SELECT MAX(Version) FROM EavFormTemplateVersions WHERE FormTemplateId = t.{nameof(EavFormTemplate.Id)} AND IsDeleted = 0
                     )
                     LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                     LEFT JOIN EquipmentTypes et ON t.{nameof(EavFormTemplate.EquipmentTypeId)} = et.Id
                     WHERE t.IsDeleted = 0
                       AND t.{nameof(EavFormTemplate.FormType)} = 'FORM'";

        if (statuses is { Length: > 0 })
        {
            sql += $" AND t.{nameof(EavFormTemplate.Status)} IN :Statuses";
        }

        sql += $" ORDER BY t.{nameof(EavFormTemplate.CreatedAt)} DESC";
        return await _connection.QueryAsync<EavFormTemplate>(sql, new { Statuses = statuses });
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
                    {nameof(EavFormTemplate.EquipmentTypeId)},
                    {nameof(EavFormTemplate.GridTypeId)},
                    {nameof(EavFormTemplate.IsActive)}, 
                    {nameof(EavFormTemplate.CreatedAt)}, 
                    {nameof(EavFormTemplate.CreatedBy)},
                    {nameof(EavFormTemplate.Status)},
                    {nameof(EavFormTemplate.FormType)},
                    IsDeleted
                )
                VALUES (:Id, :Name, :Code, :Category, :Description, :DescriptionInfo, :ExtractionProcess, :EquipmentTypeId, :GridTypeId, :IsActive, :CreatedAt, :CreatedBy, :Status, :FormType, :IsDeleted)";

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

    // Version management methods
    public async Task AddVersionAsync(EavFormTemplateVersion version)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = @"INSERT INTO EavFormTemplateVersions (Id, FormTemplateId, Code, Name, Category, Description, DescriptionInfo, FormSchema, Version, IsActive, CreatedAt, CreatedBy, Status, IsDeleted)
                    VALUES (:Id, :FormTemplateId, :Code, :Name, :Category, :Description, :DescriptionInfo, :FormSchema, :Version, :IsActive, :CreatedAt, :CreatedBy, :Status, :IsDeleted)";
        var param = new DynamicParameters();
        param.Add("Id", version.Id.ToString());
        param.Add("FormTemplateId", version.FormTemplateId.ToString());
        param.Add("Code", version.Code);
        param.Add("Name", version.Name);
        param.Add("Category", version.Category);
        param.Add("Description", version.Description);
        param.Add("DescriptionInfo", version.DescriptionInfo);
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
        var sql = @"UPDATE EavFormTemplateVersions SET IsActive = 0 WHERE FormTemplateId = :FormTemplateId";
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
}
