using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class SystemParamRepository : ISystemParamRepository
{
    private readonly IDbConnection _connection;

    public SystemParamRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<SystemParam>> GetAllAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(SystemParam.ParamKey)}, 
                   {nameof(SystemParam.ParamValue)}, 
                   {nameof(SystemParam.Description)}, 
                   {nameof(SystemParam.DataType)} 
            FROM SYSTEM_PARAM 
            ORDER BY {nameof(SystemParam.ParamKey)}";
        return await _connection.QueryAsync<SystemParam>(sql);
    }

    public async Task<SystemParam?> GetByKeyAsync(string key)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            SELECT {nameof(SystemParam.ParamKey)}, 
                   {nameof(SystemParam.ParamValue)}, 
                   {nameof(SystemParam.Description)}, 
                   {nameof(SystemParam.DataType)} 
            FROM SYSTEM_PARAM 
            WHERE {nameof(SystemParam.ParamKey)} = :ParamKey";
        return await _connection.QuerySingleOrDefaultAsync<SystemParam>(sql, new { ParamKey = key });
    }

    public async Task<bool> UpdateAsync(SystemParam systemParam)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
        var sql = $@"
            UPDATE SYSTEM_PARAM 
            SET {nameof(SystemParam.ParamValue)} = :ParamValue, 
                {nameof(SystemParam.Description)} = :Description,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE {nameof(SystemParam.ParamKey)} = :ParamKey";
        var affected = await _connection.ExecuteAsync(sql, new 
        {
            systemParam.ParamValue,
            systemParam.Description,
            systemParam.ParamKey
        });
        return affected > 0;
    }
}
