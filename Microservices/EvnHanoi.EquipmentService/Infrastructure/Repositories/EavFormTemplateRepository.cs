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

        var sql = $@"SELECT t.*, gt.Name as {nameof(EavFormTemplate.GridTypeName)}, et.Name as {nameof(EavFormTemplate.EquipmentTypeName)} 
                     FROM {nameof(EavFormTemplate)}s t
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
        var sql = $@"SELECT t.*, gt.Name as {nameof(EavFormTemplate.GridTypeName)}, et.Name as {nameof(EavFormTemplate.EquipmentTypeName)} 
                     FROM {nameof(EavFormTemplate)}s t
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

        var sql = $@"SELECT t.*, gt.Name as {nameof(EavFormTemplate.GridTypeName)}, et.Name as {nameof(EavFormTemplate.EquipmentTypeName)}
                     FROM {nameof(EavFormTemplate)}s t
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
                    {nameof(EavFormTemplate.FormSchema)}, 
                    {nameof(EavFormTemplate.EquipmentTypeId)},
                    {nameof(EavFormTemplate.GridTypeId)},
                    {nameof(EavFormTemplate.Version)}, 
                    {nameof(EavFormTemplate.IsActive)}, 
                    {nameof(EavFormTemplate.CreatedAt)}, 
                    {nameof(EavFormTemplate.CreatedBy)},
                    {nameof(EavFormTemplate.Status)},
                    {nameof(EavFormTemplate.FormType)},
                    IsDeleted
                )
                VALUES (:Id, :Name, :Code, :Category, :Description, :DescriptionInfo, :ExtractionProcess, :FormSchema, :EquipmentTypeId, :GridTypeId, :Version, :IsActive, :CreatedAt, :CreatedBy, :Status, :FormType, :IsDeleted)";

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
                         {nameof(EavFormTemplate.FormSchema)} = :FormSchema,
                         {nameof(EavFormTemplate.EquipmentTypeId)} = :EquipmentTypeId,
                         {nameof(EavFormTemplate.GridTypeId)} = :GridTypeId,
                         {nameof(EavFormTemplate.Version)} = :Version,
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
        parameters.Add("FormSchema", OracleClob.Param(template.FormSchema));
        parameters.Add("EquipmentTypeId", template.EquipmentTypeId?.ToString());
        parameters.Add("GridTypeId", template.GridTypeId);
        parameters.Add("Version", template.Version);
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
        var sql = $@"SELECT t.*, gt.Name as {nameof(EavFormTemplate.GridTypeName)}, et.Name as {nameof(EavFormTemplate.EquipmentTypeName)}, u.FullName as CreatorFullName
                     FROM {nameof(EavFormTemplate)}s t
                     LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                     LEFT JOIN EquipmentTypes et ON t.{nameof(EavFormTemplate.EquipmentTypeId)} = et.Id
                     LEFT JOIN APP_USER u ON (t.{nameof(EavFormTemplate.CreatedBy)} = u.Id OR t.{nameof(EavFormTemplate.CreatedBy)} = u.UserName)
                     WHERE t.{nameof(EavFormTemplate.Code)} = :Code AND t.IsDeleted = 0
                     ORDER BY t.{nameof(EavFormTemplate.Version)} DESC";

        return await _connection.QueryAsync<EavFormTemplate>(sql, new { Code = code });
    }
}
