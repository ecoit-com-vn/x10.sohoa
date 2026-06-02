using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class CatalogRepository : ICatalogRepository
{
    private readonly IDbConnection _connection;

    public CatalogRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<IEnumerable<Catalog>> GetAllAsync(long? unitId = null)
    {
        var sql = $"SELECT * FROM {nameof(Catalog)} WHERE {nameof(Catalog.UnitId)} IS NULL";
        if (unitId.HasValue)
        {
            sql += $" OR {nameof(Catalog.UnitId)} = :UnitId";
        }
        sql += $" ORDER BY {nameof(Catalog.CreatedAt)} DESC";
        return await _connection.QueryAsync<Catalog>(sql, new { UnitId = unitId });
    }

    public async Task<Catalog?> GetByIdAsync(long id)
    {
        var sql = $"SELECT * FROM {nameof(Catalog)} WHERE {nameof(Catalog.Id)} = :Id";
        return await _connection.QuerySingleOrDefaultAsync<Catalog>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(Catalog catalog)
    {
        var sql = $@"
            INSERT INTO {nameof(Catalog)} (
                {nameof(Catalog.Code)}, 
                {nameof(Catalog.Name)}, 
                {nameof(Catalog.CatalogType)}, 
                {nameof(Catalog.ParentId)}, 
                {nameof(Catalog.Description)}, 
                {nameof(Catalog.UnitId)}, 
                {nameof(Catalog.CreatedBy)}
            )
            VALUES (:Code, :Name, :CatalogType, :ParentId, :Description, :UnitId, :CreatedBy)
            RETURNING {nameof(Catalog.Id)} INTO :Id";
            
        var parameters = new DynamicParameters(catalog);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(Catalog catalog)
    {
        var sql = $@"
            UPDATE {nameof(Catalog)} SET 
                {nameof(Catalog.Code)} = :Code, 
                {nameof(Catalog.Name)} = :Name, 
                {nameof(Catalog.CatalogType)} = :CatalogType, 
                {nameof(Catalog.ParentId)} = :ParentId, 
                {nameof(Catalog.Description)} = :Description, 
                {nameof(Catalog.UnitId)} = :UnitId, 
                {nameof(Catalog.UpdatedAt)} = CURRENT_TIMESTAMP, 
                {nameof(Catalog.UpdatedBy)} = :UpdatedBy
            WHERE {nameof(Catalog.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, catalog);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var sql = $"DELETE FROM {nameof(Catalog)} WHERE {nameof(Catalog.Id)} = :Id";
        var affected = await _connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
