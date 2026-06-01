using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class UnitRepository : IUnitRepository
{
    private readonly string _connectionString;

    public UnitRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<IEnumerable<Unit>> GetAllAsync()
    {
        using var connection = CreateConnection();
        var sql = "SELECT Id, Code, Name, ParentId, Description FROM ORGANIZATION_UNIT ORDER BY Id";
        return await connection.QueryAsync<Unit>(sql);
    }

    public async Task<Unit?> GetByIdAsync(long id)
    {
        using var connection = CreateConnection();
        var sql = "SELECT Id, Code, Name, ParentId, Description FROM ORGANIZATION_UNIT WHERE Id = :Id";
        return await connection.QuerySingleOrDefaultAsync<Unit>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(Unit unit)
    {
        using var connection = CreateConnection();
        var sql = @"
            INSERT INTO ORGANIZATION_UNIT (Code, Name, ParentId, Description)
            VALUES (:Code, :Name, :ParentId, :Description)
            RETURNING Id INTO :Id";
            
        var parameters = new DynamicParameters();
        parameters.Add("Code", unit.Code);
        parameters.Add("Name", unit.Name);
        parameters.Add("ParentId", unit.ParentId);
        parameters.Add("Description", unit.Description);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        
        await connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(Unit unit)
    {
        using var connection = CreateConnection();
        var sql = @"
            UPDATE ORGANIZATION_UNIT 
            SET Code = :Code, 
                Name = :Name, 
                ParentId = :ParentId,
                Description = :Description,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE Id = :Id";
        var affected = await connection.ExecuteAsync(sql, new 
        {
            unit.Code,
            unit.Name,
            unit.ParentId,
            unit.Description,
            unit.Id
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var connection = CreateConnection();
        var sql = "DELETE FROM ORGANIZATION_UNIT WHERE Id = :Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
