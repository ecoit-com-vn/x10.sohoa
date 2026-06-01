using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class EquipmentRepository : IEquipmentRepository
{
    private readonly string _connectionString;

    public EquipmentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<Equipment?> GetByIdAsync(Guid id)
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM Equipments WHERE Id = :Id";
        return await connection.QuerySingleOrDefaultAsync<Equipment>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Equipment>> GetAllAsync(IEnumerable<long>? unitIds = null)
    {
        using var connection = CreateConnection();
        if (unitIds == null || !unitIds.Any())
        {
            var sql = "SELECT * FROM Equipments";
            return await connection.QueryAsync<Equipment>(sql);
        }
        else
        {
            var sql = "SELECT * FROM Equipments WHERE UnitId IN :UnitIds";
            return await connection.QueryAsync<Equipment>(sql, new { UnitIds = unitIds.ToArray() });
        }
    }

    public async Task<bool> CreateWithAttributesAsync(Equipment equipment, IEnumerable<AttributeValue> attributes)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var insertEquipmentSql = @"INSERT INTO Equipments (Id, EquipmentTypeId, Name, Code, SerialNumber, CreatedAt, CreatedBy, UnitId)
                                       VALUES (:Id, :EquipmentTypeId, :Name, :Code, :SerialNumber, :CreatedAt, :CreatedBy, :UnitId)";
            await connection.ExecuteAsync(insertEquipmentSql, equipment, transaction);

            if (attributes != null && attributes.Any())
            {
                var insertAttributeSql = @"INSERT INTO AttributeValues (Id, EquipmentId, AttributeDefinitionId, Value)
                                           VALUES (:Id, :EquipmentId, :AttributeDefinitionId, :Value)";
                await connection.ExecuteAsync(insertAttributeSql, attributes, transaction);
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

    public async Task<bool> UpdateAsync(Equipment equipment)
    {
        using var connection = CreateConnection();
        var sql = @"UPDATE Equipments 
                    SET EquipmentTypeId = :EquipmentTypeId,
                        Name = :Name,
                        Code = :Code,
                        SerialNumber = :SerialNumber,
                        UnitId = :UnitId
                    WHERE Id = :Id";
        var result = await connection.ExecuteAsync(sql, equipment);
        return result > 0;
    }

    public async Task<bool> UpdateAttributesAsync(Guid equipmentId, IEnumerable<AttributeValue> attributes)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var deleteSql = "DELETE FROM AttributeValues WHERE EquipmentId = :EquipmentId";
            await connection.ExecuteAsync(deleteSql, new { EquipmentId = equipmentId }, transaction);

            if (attributes != null && attributes.Any())
            {
                var insertSql = @"INSERT INTO AttributeValues (Id, EquipmentId, AttributeDefinitionId, Value)
                                  VALUES (:Id, :EquipmentId, :AttributeDefinitionId, :Value)";
                await connection.ExecuteAsync(insertSql, attributes, transaction);
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

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var connection = CreateConnection();
        var sql = "DELETE FROM Equipments WHERE Id = :Id";
        var result = await connection.ExecuteAsync(sql, new { Id = id });
        return result > 0;
    }

    public async Task<IEnumerable<AttributeValue>> GetAttributesAsync(Guid equipmentId)
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM AttributeValues WHERE EquipmentId = :EquipmentId";
        return await connection.QueryAsync<AttributeValue>(sql, new { EquipmentId = equipmentId });
    }
}
