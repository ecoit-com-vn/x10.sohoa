using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class EquipmentTypeRepository : IEquipmentTypeRepository
{
    private readonly string _connectionString;

    public EquipmentTypeRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<EquipmentType?> GetByIdAsync(Guid id)
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM EquipmentTypes WHERE Id = :Id";
        return await connection.QuerySingleOrDefaultAsync<EquipmentType>(sql, new { Id = id });
    }

    public async Task<IEnumerable<EquipmentType>> GetAllAsync()
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM EquipmentTypes";
        return await connection.QueryAsync<EquipmentType>(sql);
    }

    public async Task<bool> CreateAsync(EquipmentType type)
    {
        using var connection = CreateConnection();
        var sql = @"INSERT INTO EquipmentTypes (Id, Name, Code, Description)
                    VALUES (:Id, :Name, :Code, :Description)";
        var result = await connection.ExecuteAsync(sql, type);
        return result > 0;
    }

    public async Task<bool> UpdateAsync(EquipmentType type)
    {
        using var connection = CreateConnection();
        var sql = @"UPDATE EquipmentTypes 
                    SET Name = :Name,
                        Code = :Code,
                        Description = :Description
                    WHERE Id = :Id";
        var result = await connection.ExecuteAsync(sql, type);
        return result > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var connection = CreateConnection();
        var sql = "DELETE FROM EquipmentTypes WHERE Id = :Id";
        var result = await connection.ExecuteAsync(sql, new { Id = id });
        return result > 0;
    }

    public async Task<IEnumerable<AttributeDefinition>> GetAttributeDefinitionsAsync(Guid equipmentTypeId)
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM AttributeDefinitions WHERE EquipmentTypeId = :EquipmentTypeId";
        return await connection.QueryAsync<AttributeDefinition>(sql, new { EquipmentTypeId = equipmentTypeId });
    }

    public async Task<bool> AddAttributeDefinitionAsync(AttributeDefinition attributeDefinition)
    {
        using var connection = CreateConnection();
        var sql = @"INSERT INTO AttributeDefinitions (Id, EquipmentTypeId, Name, Code, DataType, IsRequired)
                    VALUES (:Id, :EquipmentTypeId, :Name, :Code, :DataType, :IsRequired)";
        var result = await connection.ExecuteAsync(sql, attributeDefinition);
        return result > 0;
    }
}
