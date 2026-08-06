using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class ExternalApiKeyValidator : IExternalApiKeyValidator
{
    private readonly IDbConnection _connection;

    public ExternalApiKeyValidator(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<long?> ValidateAsync(string keyName, string keyHash)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            SELECT ID
            FROM EXTERNAL_API_KEYS
            WHERE KEY_NAME = :KeyName
              AND KEY_HASH = :KeyHash
              AND IS_ACTIVE = 1
              AND (EXPIRES_AT IS NULL OR EXPIRES_AT > SYSTIMESTAMP)";

        return await _connection.QuerySingleOrDefaultAsync<long?>(sql, new { KeyName = keyName, KeyHash = keyHash });
    }
}
