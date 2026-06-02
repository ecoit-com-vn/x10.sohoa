using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class EquipmentRepository : IEquipmentRepository
{
    private readonly IDbConnection _connection;

    public EquipmentRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<Equipment?> GetByIdAsync(Guid id)
    {
        var sql = $"SELECT * FROM {nameof(Equipment)}s WHERE {nameof(Equipment.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<Equipment>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Equipment>> GetAllAsync(IEnumerable<long>? unitIds = null)
    {
        if (unitIds == null || !unitIds.Any())
        {
            var sql = $"SELECT * FROM {nameof(Equipment)}s";
            return await _connection.QueryAsync<Equipment>(sql);
        }
        else
        {
            var sql = $"SELECT * FROM {nameof(Equipment)}s WHERE {nameof(Equipment.UnitId)} IN :UnitIds";
            return await _connection.QueryAsync<Equipment>(sql, new { UnitIds = unitIds.ToArray() });
        }
    }

    public async Task<bool> CreateWithAttributesAsync(Equipment equipment, IEnumerable<AttributeValue> attributes)
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
        using var transaction = _connection.BeginTransaction();

        try
        {
            var insertEquipmentSql = $@"INSERT INTO {nameof(Equipment)}s (
                                           {nameof(Equipment.Id)}, 
                                           {nameof(Equipment.EquipmentTypeId)}, 
                                           {nameof(Equipment.Name)}, 
                                           {nameof(Equipment.Code)}, 
                                           {nameof(Equipment.SerialNumber)}, 
                                           {nameof(Equipment.CreatedAt)}, 
                                           {nameof(Equipment.CreatedBy)}, 
                                           {nameof(Equipment.UnitId)}
                                       )
                                       VALUES (:Id, :EquipmentTypeId, :Name, :Code, :SerialNumber, :CreatedAt, :CreatedBy, :UnitId)";
            await _connection.ExecuteAsync(insertEquipmentSql, equipment, transaction);

            if (attributes != null && attributes.Any())
            {
                var insertAttributeSql = $@"INSERT INTO {nameof(AttributeValue)}s (
                                               {nameof(AttributeValue.Id)}, 
                                               {nameof(AttributeValue.EquipmentId)}, 
                                               {nameof(AttributeValue.AttributeDefinitionId)}, 
                                               {nameof(AttributeValue.Value)}
                                           )
                                           VALUES (:Id, :EquipmentId, :AttributeDefinitionId, :Value)";
                await _connection.ExecuteAsync(insertAttributeSql, attributes, transaction);
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
        var sql = $@"UPDATE {nameof(Equipment)}s 
                    SET {nameof(Equipment.EquipmentTypeId)} = :EquipmentTypeId,
                        {nameof(Equipment.Name)} = :Name,
                        {nameof(Equipment.Code)} = :Code,
                        {nameof(Equipment.SerialNumber)} = :SerialNumber,
                        {nameof(Equipment.UnitId)} = :UnitId
                    WHERE {nameof(Equipment.Id)} = :Id";
        var result = await _connection.ExecuteAsync(sql, equipment);
        return result > 0;
    }

    public async Task<bool> UpdateAttributesAsync(Guid equipmentId, IEnumerable<AttributeValue> attributes)
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
        using var transaction = _connection.BeginTransaction();
        try
        {
            var deleteSql = $"DELETE FROM {nameof(AttributeValue)}s WHERE {nameof(AttributeValue.EquipmentId)} = :EquipmentId";
            await _connection.ExecuteAsync(deleteSql, new { EquipmentId = equipmentId }, transaction);

            if (attributes != null && attributes.Any())
            {
                var insertSql = $@"INSERT INTO {nameof(AttributeValue)}s (
                                      {nameof(AttributeValue.Id)}, 
                                      {nameof(AttributeValue.EquipmentId)}, 
                                      {nameof(AttributeValue.AttributeDefinitionId)}, 
                                      {nameof(AttributeValue.Value)}
                                  )
                                  VALUES (:Id, :EquipmentId, :AttributeDefinitionId, :Value)";
                await _connection.ExecuteAsync(insertSql, attributes, transaction);
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
        var sql = $"DELETE FROM {nameof(Equipment)}s WHERE {nameof(Equipment.Id)} = :Id";
        var result = await _connection.ExecuteAsync(sql, new { Id = id });
        return result > 0;
    }

    public async Task<IEnumerable<AttributeValue>> GetAttributesAsync(Guid equipmentId)
    {
        var sql = $"SELECT * FROM {nameof(AttributeValue)}s WHERE {nameof(AttributeValue.EquipmentId)} = :EquipmentId";
        return await _connection.QueryAsync<AttributeValue>(sql, new { EquipmentId = equipmentId });
    }
}
