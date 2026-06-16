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
        var sql = $"SELECT * FROM {nameof(EavFormTemplate)}s WHERE {nameof(EavFormTemplate.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<EavFormTemplate>(sql, new { Id = id.ToString() });
    }

    public async Task<IEnumerable<EavFormTemplate>> GetAllActiveAsync()
    {
        var sql = $"SELECT * FROM {nameof(EavFormTemplate)}s WHERE {nameof(EavFormTemplate.IsActive)} = 1";
        return await _connection.QueryAsync<EavFormTemplate>(sql);
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
                    {nameof(EavFormTemplate.FormSchema)}, 
                    {nameof(EavFormTemplate.Version)}, 
                    {nameof(EavFormTemplate.IsActive)}, 
                    {nameof(EavFormTemplate.CreatedAt)}, 
                    {nameof(EavFormTemplate.CreatedBy)},
                    {nameof(EavFormTemplate.Status)}
                )
                VALUES (:Id, :Name, :Code, :Category, :Description, :DescriptionInfo, :FormSchema, :Version, :IsActive, :CreatedAt, :CreatedBy, :Status)";

        var param = new
        {
            Id = template.Id.ToString(),
            template.Name,
            template.Code,
            template.Category,
            template.Description,
            template.DescriptionInfo,
            template.FormSchema,
            template.Version,
            IsActive = template.IsActive ? 1 : 0,
            template.CreatedAt,
            template.CreatedBy,
            template.Status
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
                        {nameof(EavFormTemplate.FormSchema)} = :FormSchema,
                        {nameof(EavFormTemplate.Version)} = :Version,
                        {nameof(EavFormTemplate.IsActive)} = :IsActive,
                        {nameof(EavFormTemplate.Status)} = :Status
                    WHERE {nameof(EavFormTemplate.Id)} = :Id";
        
        var param = new
        {
            template.Name,
            template.Code,
            template.Category,
            template.Description,
            template.DescriptionInfo,
            template.FormSchema,
            template.Version,
            IsActive = template.IsActive ? 1 : 0,
            template.Status,
            Id = template.Id.ToString()
        };

        await _connection.ExecuteAsync(sql, param);
    }
}
