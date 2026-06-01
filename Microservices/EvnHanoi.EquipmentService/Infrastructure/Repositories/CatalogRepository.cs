using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class CatalogRepository : ICatalogRepository
{
    private readonly string _connectionString;

    public CatalogRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<IEnumerable<Catalog>> GetAllAsync(long? unitId = null)
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM CATALOG WHERE UnitId IS NULL";
        if (unitId.HasValue)
        {
            sql += " OR UnitId = :UnitId";
        }
        sql += " ORDER BY CreatedAt DESC";
        return await connection.QueryAsync<Catalog>(sql, new { UnitId = unitId });
    }

    public async Task<Catalog?> GetByIdAsync(long id)
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM CATALOG WHERE Id = :Id";
        return await connection.QuerySingleOrDefaultAsync<Catalog>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(Catalog catalog)
    {
        using var connection = CreateConnection();
        var sql = @"
            INSERT INTO CATALOG (Code, Name, CatalogType, ParentId, Description, UnitId, CreatedBy)
            VALUES (:Code, :Name, :CatalogType, :ParentId, :Description, :UnitId, :CreatedBy)
            RETURNING Id INTO :Id";
            
        var parameters = new DynamicParameters(catalog);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(Catalog catalog)
    {
        using var connection = CreateConnection();
        var sql = @"
            UPDATE CATALOG SET 
                Code = :Code, 
                Name = :Name, 
                CatalogType = :CatalogType, 
                ParentId = :ParentId, 
                Description = :Description, 
                UnitId = :UnitId, 
                UpdatedAt = CURRENT_TIMESTAMP, 
                UpdatedBy = :UpdatedBy
            WHERE Id = :Id";
        var affected = await connection.ExecuteAsync(sql, catalog);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var connection = CreateConnection();
        var sql = "DELETE FROM CATALOG WHERE Id = :Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
