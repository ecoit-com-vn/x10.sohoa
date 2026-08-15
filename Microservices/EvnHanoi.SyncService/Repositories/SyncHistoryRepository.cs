using System.Data;
using Dapper;
using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Repositories;

public class SyncHistoryRepository : ISyncHistoryRepository
{
    private readonly IDbConnection _connection;

    public SyncHistoryRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<string> CreateAsync(SyncHistory history)
    {
        EnsureOpen();
        var id = string.IsNullOrWhiteSpace(history.Id) ? Guid.CreateVersion7().ToString() : history.Id;

        const string sql = @"
            INSERT INTO SYNC_HISTORY (
                ID, SYNC_CONFIG_ID, OBJECT_TYPE, SYNC_TYPE, START_TIME, STATUS,
                TOTAL_RECORDS, SUCCESS_RECORDS, FAILED_RECORDS, CREATED_BY
            ) VALUES (
                :Id, :SyncConfigId, :ObjectType, :SyncType, :StartTime, :Status,
                0, 0, 0, :CreatedBy
            )";

        await _connection.ExecuteAsync(sql, new
        {
            Id = id,
            history.SyncConfigId,
            history.ObjectType,
            history.SyncType,
            history.StartTime,
            history.Status,
            history.CreatedBy
        });
        return id;
    }

    public async Task CompleteAsync(string id, string status, int totalRecords, int successRecords, int failedRecords, string? errorMessage)
    {
        EnsureOpen();
        const string sql = @"
            UPDATE SYNC_HISTORY
            SET STATUS = :Status, END_TIME = SYSTIMESTAMP,
                TOTAL_RECORDS = :TotalRecords, SUCCESS_RECORDS = :SuccessRecords, FAILED_RECORDS = :FailedRecords,
                ERROR_MESSAGE = :ErrorMessage
            WHERE ID = :Id";
        await _connection.ExecuteAsync(sql, new { Id = id, Status = status, TotalRecords = totalRecords, SuccessRecords = successRecords, FailedRecords = failedRecords, ErrorMessage = errorMessage });
    }

    public async Task InsertDetailsAsync(IEnumerable<SyncHistoryDetail> details)
    {
        EnsureOpen();
        const string sql = @"
            INSERT INTO SYNC_HISTORY_DETAIL (
                ID, SYNC_HISTORY_ID, SOURCE_ID, SOURCE_CODE, SOURCE_NAME, TARGET_ID,
                ACTION_TYPE, STATUS, DATA_CONTENT, ERROR_MESSAGE, SYNC_TIME
            ) VALUES (
                :Id, :SyncHistoryId, :SourceId, :SourceCode, :SourceName, :TargetId,
                :ActionType, :Status, :DataContent, :ErrorMessage, SYSTIMESTAMP
            )";

        foreach (var detail in details)
        {
            await _connection.ExecuteAsync(sql, new
            {
                Id = string.IsNullOrWhiteSpace(detail.Id) ? Guid.CreateVersion7().ToString() : detail.Id,
                detail.SyncHistoryId,
                detail.SourceId,
                detail.SourceCode,
                detail.SourceName,
                detail.TargetId,
                detail.ActionType,
                detail.Status,
                detail.DataContent,
                detail.ErrorMessage
            });
        }
    }

    public async Task<(IEnumerable<SyncHistory> Items, int TotalCount)> GetPagedAsync(string? objectType, int page, int pageSize)
    {
        EnsureOpen();
        var whereClause = string.IsNullOrWhiteSpace(objectType) ? "" : "WHERE OBJECT_TYPE = :ObjectType";
        var parameters = new DynamicParameters();
        parameters.Add("ObjectType", objectType);
        parameters.Add("Skip", (page - 1) * pageSize);
        parameters.Add("Take", pageSize);

        var totalCount = await _connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM SYNC_HISTORY {whereClause}", parameters);

        var items = await _connection.QueryAsync<SyncHistory>($@"
            SELECT ID AS Id, SYNC_CONFIG_ID AS SyncConfigId, OBJECT_TYPE AS ObjectType, SYNC_TYPE AS SyncType,
                   START_TIME AS StartTime, END_TIME AS EndTime, STATUS AS Status,
                   TOTAL_RECORDS AS TotalRecords, SUCCESS_RECORDS AS SuccessRecords, FAILED_RECORDS AS FailedRecords,
                   ERROR_MESSAGE AS ErrorMessage, CREATED_BY AS CreatedBy
            FROM SYNC_HISTORY
            {whereClause}
            ORDER BY START_TIME DESC
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY", parameters);

        return (items, totalCount);
    }

    public async Task<(IEnumerable<SyncHistoryDetail> Items, int TotalCount)> GetDetailsPagedAsync(string syncHistoryId, int page, int pageSize)
    {
        EnsureOpen();
        var parameters = new DynamicParameters();
        parameters.Add("SyncHistoryId", syncHistoryId);
        parameters.Add("Skip", (page - 1) * pageSize);
        parameters.Add("Take", pageSize);

        var totalCount = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM SYNC_HISTORY_DETAIL WHERE SYNC_HISTORY_ID = :SyncHistoryId", parameters);

        var items = await _connection.QueryAsync<SyncHistoryDetail>(@"
            SELECT ID AS Id, SYNC_HISTORY_ID AS SyncHistoryId, SOURCE_ID AS SourceId, SOURCE_CODE AS SourceCode,
                   SOURCE_NAME AS SourceName, TARGET_ID AS TargetId, ACTION_TYPE AS ActionType, STATUS AS Status,
                   DATA_CONTENT AS DataContent, ERROR_MESSAGE AS ErrorMessage, SYNC_TIME AS SyncTime
            FROM SYNC_HISTORY_DETAIL
            WHERE SYNC_HISTORY_ID = :SyncHistoryId
            ORDER BY SYNC_TIME DESC
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY", parameters);

        return (items, totalCount);
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }
}
