using System.Data;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class ExternalApiKeyRepository : IExternalApiKeyRepository
{
    private readonly IDbConnection _connection;

    public ExternalApiKeyRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<ExternalApiKey>> GetAllAsync()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = @"
            SELECT k.ID AS Id,
                   k.KEY_NAME AS KeyName,
                   k.IS_ACTIVE AS IsActive,
                   k.EXPIRES_AT AS ExpiresAt,
                   k.CREATED_AT AS CreatedAt,
                   COALESCE(u.FullName, u.Username, k.CREATED_BY) AS CreatedBy,
                   k.REVOKED_AT AS RevokedAt,
                   k.NOTE AS Note
            FROM EXTERNAL_API_KEYS k
            LEFT JOIN APP_USER u ON u.Id = k.CREATED_BY
            ORDER BY k.CREATED_AT DESC, k.ID DESC";
        return await _connection.QueryAsync<ExternalApiKey>(sql);
    }

    public async Task<ExternalApiKey?> GetByIdAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = @"
            SELECT k.ID AS Id,
                   k.KEY_NAME AS KeyName,
                   k.KEY_HASH AS KeyHash,
                   k.ENCRYPTED_KEY AS EncryptedKey,
                   k.IS_ACTIVE AS IsActive,
                   k.EXPIRES_AT AS ExpiresAt,
                   k.CREATED_AT AS CreatedAt,
                   COALESCE(u.FullName, u.Username, k.CREATED_BY) AS CreatedBy,
                   k.REVOKED_AT AS RevokedAt,
                   k.NOTE AS Note
            FROM EXTERNAL_API_KEYS k
            LEFT JOIN APP_USER u ON u.Id = k.CREATED_BY
            WHERE k.ID = :Id";
        return await _connection.QuerySingleOrDefaultAsync<ExternalApiKey>(sql, new { Id = id });
    }

    public async Task<long> CreateAsync(ExternalApiKey apiKey)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = @"
            INSERT INTO EXTERNAL_API_KEYS (
                KEY_NAME, KEY_HASH, ENCRYPTED_KEY, IS_ACTIVE, EXPIRES_AT, CREATED_BY, REVOKED_AT, NOTE
            ) VALUES (
                :KeyName, :KeyHash, :EncryptedKey, :IsActive, :ExpiresAt, :CreatedBy, :RevokedAt, :Note
            ) RETURNING ID INTO :Id";

        var parameters = new DynamicParameters();
        parameters.Add("KeyName", apiKey.KeyName);
        parameters.Add("KeyHash", apiKey.KeyHash);
        parameters.Add("EncryptedKey", apiKey.EncryptedKey);
        parameters.Add("IsActive", apiKey.IsActive ? 1 : 0);
        parameters.Add("ExpiresAt", apiKey.ExpiresAt);
        parameters.Add("CreatedBy", apiKey.CreatedBy);
        parameters.Add("RevokedAt", apiKey.RevokedAt);
        parameters.Add("Note", apiKey.Note);
        parameters.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await _connection.ExecuteAsync(sql, parameters);
        return parameters.Get<long>("Id");
    }

    public async Task<bool> UpdateAsync(ExternalApiKey apiKey)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = @"
            UPDATE EXTERNAL_API_KEYS
            SET KEY_NAME = :KeyName,
                IS_ACTIVE = :IsActive,
                EXPIRES_AT = :ExpiresAt,
                REVOKED_AT = :RevokedAt,
                NOTE = :Note
            WHERE ID = :Id";

        var affected = await _connection.ExecuteAsync(sql, new
        {
            apiKey.Id,
            apiKey.KeyName,
            IsActive = apiKey.IsActive ? 1 : 0,
            apiKey.ExpiresAt,
            apiKey.RevokedAt,
            apiKey.Note
        });
        return affected > 0;
    }   

    public async Task<bool> UpdateKeyValueAsync(long id, string keyHash, string encryptedKey)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var affected = await _connection.ExecuteAsync(@"
            UPDATE EXTERNAL_API_KEYS
            SET KEY_HASH = :KeyHash,
                ENCRYPTED_KEY = :EncryptedKey
            WHERE ID = :Id",
            new { Id = id, KeyHash = keyHash, EncryptedKey = encryptedKey });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var affected = await _connection.ExecuteAsync(
            "DELETE FROM EXTERNAL_API_KEYS WHERE ID = :Id",
            new { Id = id });
        return affected > 0;
    }
}
