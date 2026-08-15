using System.Data;
using Dapper;
using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Repositories;

public class SyncConfigRepository : ISyncConfigRepository
{
    private readonly IDbConnection _connection;

    public SyncConfigRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<SyncConfig>> GetAllAsync()
    {
        EnsureOpen();
        const string sql = @"
            SELECT ID AS Id, OBJECT_TYPE AS ObjectType, FREQUENCY_VALUE AS FrequencyValue,
                   FREQUENCY_UNIT AS FrequencyUnit, IS_ENABLED AS IsEnabled,
                   LAST_SYNC_AT AS LastSyncAt, NEXT_SYNC_AT AS NextSyncAt, ROW_VERSION AS RowVersion
            FROM SYNC_CONFIG
            WHERE IS_DELETED = 0
            ORDER BY OBJECT_TYPE";
        return await _connection.QueryAsync<SyncConfig>(sql);
    }

    public async Task<SyncConfig?> GetByObjectTypeAsync(string objectType)
    {
        EnsureOpen();
        const string sql = @"
            SELECT ID AS Id, OBJECT_TYPE AS ObjectType, FREQUENCY_VALUE AS FrequencyValue,
                   FREQUENCY_UNIT AS FrequencyUnit, IS_ENABLED AS IsEnabled,
                   LAST_SYNC_AT AS LastSyncAt, NEXT_SYNC_AT AS NextSyncAt, ROW_VERSION AS RowVersion
            FROM SYNC_CONFIG
            WHERE OBJECT_TYPE = :ObjectType AND IS_DELETED = 0";
        return await _connection.QuerySingleOrDefaultAsync<SyncConfig>(sql, new { ObjectType = objectType });
    }

    public async Task<bool> UpdateAsync(string objectType, UpdateSyncConfigRequest request, string? modifiedBy)
    {
        EnsureOpen();
        const string sql = @"
            UPDATE SYNC_CONFIG
            SET IS_ENABLED = :IsEnabled,
                FREQUENCY_VALUE = :FrequencyValue,
                FREQUENCY_UNIT = :FrequencyUnit,
                ROW_VERSION = ROW_VERSION + 1,
                UPDATED_BY = :ModifiedBy,
                UPDATED_AT = SYSTIMESTAMP
            WHERE OBJECT_TYPE = :ObjectType AND ROW_VERSION = :ExpectedVersion AND IS_DELETED = 0";

        var affected = await _connection.ExecuteAsync(sql, new
        {
            ObjectType = objectType,
            IsEnabled = request.IsEnabled ? 1 : 0,
            request.FrequencyValue,
            request.FrequencyUnit,
            ExpectedVersion = request.RowVersion,
            ModifiedBy = modifiedBy
        });
        return affected > 0;
    }

    public async Task UpdateRunResultAsync(string objectType, DateTime lastSyncAt, DateTime? nextSyncAt)
    {
        EnsureOpen();
        const string sql = @"
            UPDATE SYNC_CONFIG
            SET LAST_SYNC_AT = :LastSyncAt, NEXT_SYNC_AT = :NextSyncAt, ROW_VERSION = ROW_VERSION + 1
            WHERE OBJECT_TYPE = :ObjectType AND IS_DELETED = 0";
        await _connection.ExecuteAsync(sql, new { ObjectType = objectType, LastSyncAt = lastSyncAt, NextSyncAt = nextSyncAt });
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }
}
