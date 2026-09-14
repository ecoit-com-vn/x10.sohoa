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
                   LAST_SYNC_AT AS LastSyncAt, NEXT_SYNC_AT AS NextSyncAt, ROW_VERSION AS RowVersion,
                   CONSECUTIVE_FAILURE_COUNT AS ConsecutiveFailureCount
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
                   LAST_SYNC_AT AS LastSyncAt, NEXT_SYNC_AT AS NextSyncAt, ROW_VERSION AS RowVersion,
                   CONSECUTIVE_FAILURE_COUNT AS ConsecutiveFailureCount
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

    public async Task UpdateRunResultAsync(string objectType, DateTime lastSyncAt, DateTime? nextSyncAt, int consecutiveFailureCount)
    {
        EnsureOpen();
        const string sql = @"
            UPDATE SYNC_CONFIG
            SET LAST_SYNC_AT = :LastSyncAt, NEXT_SYNC_AT = :NextSyncAt,
                CONSECUTIVE_FAILURE_COUNT = :ConsecutiveFailureCount, ROW_VERSION = ROW_VERSION + 1
            WHERE OBJECT_TYPE = :ObjectType AND IS_DELETED = 0";
        await _connection.ExecuteAsync(sql, new
        {
            ObjectType = objectType,
            LastSyncAt = lastSyncAt,
            NextSyncAt = nextSyncAt,
            ConsecutiveFailureCount = consecutiveFailureCount
        });
    }

    public async Task MarkDueNowAsync(IEnumerable<string> objectTypes)
    {
        EnsureOpen();
        // Bind DateTime.UtcNow qua tham số (KHÔNG dùng SYSTIMESTAMP) — giống hệt cách UpdateRunResultAsync
        // ở trên ghi NEXT_SYNC_AT, vì PmisScheduledSyncJob so sánh cột này với DateTime.UtcNow phía .NET
        // (xem RunIfDueAsync: "config.NextSyncAt <= now"); dùng SYSTIMESTAMP sẽ lệch múi giờ nếu server
        // Oracle không chạy UTC, khiến NEXT_SYNC_AT bị ghi thành 1 mốc trong tương lai so với UtcNow và
        // job không bao giờ nhận là "đã tới hạn".
        const string sql = @"
            UPDATE SYNC_CONFIG
            SET NEXT_SYNC_AT = :NextSyncAt, ROW_VERSION = ROW_VERSION + 1
            WHERE OBJECT_TYPE = :ObjectType AND IS_ENABLED = 1 AND IS_DELETED = 0";
        var now = DateTime.UtcNow;
        foreach (var objectType in objectTypes)
        {
            await _connection.ExecuteAsync(sql, new { ObjectType = objectType, NextSyncAt = now });
        }
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }
}
