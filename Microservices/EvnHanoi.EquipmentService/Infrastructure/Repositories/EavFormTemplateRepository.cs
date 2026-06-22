using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

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
        var sql = $@"SELECT t.*, gt.Name as {nameof(EavFormTemplate.GridTypeName)} 
                     FROM {nameof(EavFormTemplate)}s t
                     LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                     WHERE t.{nameof(EavFormTemplate.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<EavFormTemplate>(sql, new { Id = id.ToString() });
    }

    public async Task<IEnumerable<EavFormTemplate>> GetAllActiveAsync(string? formType = null)
    {
        var sql = $@"SELECT t.*, gt.Name as {nameof(EavFormTemplate.GridTypeName)} 
                     FROM {nameof(EavFormTemplate)}s t
                     LEFT JOIN GridTypes gt ON t.{nameof(EavFormTemplate.GridTypeId)} = gt.Id
                     WHERE t.{nameof(EavFormTemplate.IsActive)} = 1";
        if (!string.IsNullOrEmpty(formType))
        {
            sql += $" AND t.{nameof(EavFormTemplate.FormType)} = :FormType";
        }
        return await _connection.QueryAsync<EavFormTemplate>(sql, new { FormType = formType });
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
                    {nameof(EavFormTemplate.FormType)}
                )
                VALUES (:Id, :Name, :Code, :Category, :Description, :DescriptionInfo, :ExtractionProcess, :FormSchema, :EquipmentTypeId, :GridTypeId, :Version, :IsActive, :CreatedAt, :CreatedBy, :Status, :FormType)";

        var param = new
        {
            Id = template.Id.ToString(),
            template.Name,
            template.Code,
            template.Category,
            template.Description,
            template.DescriptionInfo,
            template.ExtractionProcess,
            template.FormSchema,
            EquipmentTypeId = template.EquipmentTypeId?.ToString(),
            template.GridTypeId,
            template.Version,
            IsActive = template.IsActive ? 1 : 0,
            template.CreatedAt,
            template.CreatedBy,
            template.Status,
            template.FormType
        };

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
                        {nameof(EavFormTemplate.FormType)} = :FormType
                    WHERE {nameof(EavFormTemplate.Id)} = :Id";
        
        var param = new
        {
            template.Name,
            template.Code,
            template.Category,
            template.Description,
            template.DescriptionInfo,
            template.ExtractionProcess,
            template.FormSchema,
            EquipmentTypeId = template.EquipmentTypeId?.ToString(),
            template.GridTypeId,
            template.Version,
            IsActive = template.IsActive ? 1 : 0,
            template.Status,
            template.FormType,
            Id = template.Id.ToString()
        };

        await _connection.ExecuteAsync(sql, param);
    }
}
