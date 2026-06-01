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

public class SystemParamRepository : ISystemParamRepository
{
    private readonly string _connectionString;

    public SystemParamRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private IDbConnection CreateConnection() => new OracleConnection(_connectionString);

    public async Task<IEnumerable<SystemParam>> GetAllAsync()
    {
        using var connection = CreateConnection();
        var sql = "SELECT ParamKey, ParamValue, Description, DataType FROM SYSTEM_PARAM ORDER BY ParamKey";
        return await connection.QueryAsync<SystemParam>(sql);
    }

    public async Task<SystemParam?> GetByKeyAsync(string key)
    {
        using var connection = CreateConnection();
        var sql = "SELECT ParamKey, ParamValue, Description, DataType FROM SYSTEM_PARAM WHERE ParamKey = :ParamKey";
        return await connection.QuerySingleOrDefaultAsync<SystemParam>(sql, new { ParamKey = key });
    }

    public async Task<bool> UpdateAsync(SystemParam systemParam)
    {
        using var connection = CreateConnection();
        var sql = @"
            UPDATE SYSTEM_PARAM 
            SET ParamValue = :ParamValue, 
                Description = :Description,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE ParamKey = :ParamKey";
        var affected = await connection.ExecuteAsync(sql, new 
        {
            systemParam.ParamValue,
            systemParam.Description,
            systemParam.ParamKey
        });
        return affected > 0;
    }
}
