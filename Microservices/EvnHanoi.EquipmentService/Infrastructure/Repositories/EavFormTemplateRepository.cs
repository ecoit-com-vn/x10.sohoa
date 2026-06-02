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
        return await _connection.QuerySingleOrDefaultAsync<EavFormTemplate>(sql, new { Id = id });
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
                        {nameof(EavFormTemplate.Description)}, 
                        {nameof(EavFormTemplate.Schema)}, 
                        {nameof(EavFormTemplate.Version)}, 
                        {nameof(EavFormTemplate.IsActive)}, 
                        {nameof(EavFormTemplate.CreatedAt)}, 
                        {nameof(EavFormTemplate.CreatedBy)}
                    )
                    VALUES (:Id, :Name, :Description, :Schema, :Version, :IsActive, :CreatedAt, :CreatedBy)";
        
        var param = new
        {
            template.Id,
            template.Name,
            template.Description,
            template.Schema,
            template.Version,
            IsActive = template.IsActive ? 1 : 0,
            template.CreatedAt,
            template.CreatedBy
        };

        await _connection.ExecuteAsync(sql, param);
    }

    public async Task UpdateAsync(EavFormTemplate template)
    {
        var sql = $@"UPDATE {nameof(EavFormTemplate)}s
                    SET {nameof(EavFormTemplate.Name)} = :Name,
                        {nameof(EavFormTemplate.Description)} = :Description,
                        {nameof(EavFormTemplate.Schema)} = :Schema,
                        {nameof(EavFormTemplate.Version)} = :Version,
                        {nameof(EavFormTemplate.IsActive)} = :IsActive
                    WHERE {nameof(EavFormTemplate.Id)} = :Id";
        
        var param = new
        {
            template.Id,
            template.Name,
            template.Description,
            template.Schema,
            template.Version,
            IsActive = template.IsActive ? 1 : 0
        };

        await _connection.ExecuteAsync(sql, param);
    }
}
