using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class SystemParamRepository : ISystemParamRepository
{
    private readonly IDbConnection _connection;

    public SystemParamRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<string?> GetValueAsync(string paramKey)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = "SELECT PARAMVALUE FROM SYSTEM_PARAM WHERE PARAMKEY = :ParamKey";
        return await _connection.ExecuteScalarAsync<string>(sql, new { ParamKey = paramKey });
    }
}
