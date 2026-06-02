using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class EquipmentTypeRepository : IEquipmentTypeRepository
{
    private readonly IDbConnection _connection;

    public EquipmentTypeRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<EquipmentType?> GetByIdAsync(Guid id)
    {
        var sql = $"SELECT * FROM {nameof(EquipmentType)}s WHERE {nameof(EquipmentType.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<EquipmentType>(sql, new { Id = id });
    }

    public async Task<IEnumerable<EquipmentType>> GetAllAsync()
    {
        var sql = $"SELECT * FROM {nameof(EquipmentType)}s";
        return await _connection.QueryAsync<EquipmentType>(sql);
    }

    public async Task<bool> CreateAsync(EquipmentType type)
    {
        var sql = $@"INSERT INTO {nameof(EquipmentType)}s (
                        {nameof(EquipmentType.Id)}, 
                        {nameof(EquipmentType.Name)}, 
                        {nameof(EquipmentType.Code)}, 
                        {nameof(EquipmentType.Description)}
                    )
                    VALUES (:Id, :Name, :Code, :Description)";
        var result = await _connection.ExecuteAsync(sql, type);
        return result > 0;
    }

    public async Task<bool> UpdateAsync(EquipmentType type)
    {
        var sql = $@"UPDATE {nameof(EquipmentType)}s 
                    SET {nameof(EquipmentType.Name)} = :Name,
                        {nameof(EquipmentType.Code)} = :Code,
                        {nameof(EquipmentType.Description)} = :Description
                    WHERE {nameof(EquipmentType.Id)} = :Id";
        var result = await _connection.ExecuteAsync(sql, type);
        return result > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var sql = $"DELETE FROM {nameof(EquipmentType)}s WHERE {nameof(EquipmentType.Id)} = :Id";
        var result = await _connection.ExecuteAsync(sql, new { Id = id });
        return result > 0;
    }

    public async Task<IEnumerable<AttributeDefinition>> GetAttributeDefinitionsAsync(Guid equipmentTypeId)
    {
        var sql = $"SELECT * FROM {nameof(AttributeDefinition)}s WHERE {nameof(AttributeDefinition.EquipmentTypeId)} = :EquipmentTypeId";
        return await _connection.QueryAsync<AttributeDefinition>(sql, new { EquipmentTypeId = equipmentTypeId });
    }

    public async Task<bool> AddAttributeDefinitionAsync(AttributeDefinition attributeDefinition)
    {
        var sql = $@"INSERT INTO {nameof(AttributeDefinition)}s (
                        {nameof(AttributeDefinition.Id)}, 
                        {nameof(AttributeDefinition.EquipmentTypeId)}, 
                        {nameof(AttributeDefinition.Name)}, 
                        {nameof(AttributeDefinition.Code)}, 
                        {nameof(AttributeDefinition.DataType)}, 
                        {nameof(AttributeDefinition.IsRequired)}
                    )
                    VALUES (:Id, :EquipmentTypeId, :Name, :Code, :DataType, :IsRequired)";
        var result = await _connection.ExecuteAsync(sql, attributeDefinition);
        return result > 0;
    }
}
