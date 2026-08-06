using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class ExternalApiCallLogRepository : IExternalApiCallLogRepository
{
    private readonly IDbConnection _connection;

    public ExternalApiCallLogRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task LogAsync(ExternalApiCallLogEntry entry)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        const string sql = @"
            INSERT INTO EXTERNAL_API_CALL_LOGS (
                API_KEY_ID, KEY_NAME, ENDPOINT, HTTP_METHOD, REQUEST_QUERY, REQUEST_IP,
                STATUS_CODE, IS_SUCCESS, DURATION_MS, RESPONSE_SUMMARY, ERROR_MESSAGE
            ) VALUES (
                :ApiKeyId, :KeyName, :Endpoint, :HttpMethod, :RequestQuery, :RequestIp,
                :StatusCode, :IsSuccess, :DurationMs, :ResponseSummary, :ErrorMessage
            )";

        await _connection.ExecuteAsync(sql, new
        {
            entry.ApiKeyId,
            entry.KeyName,
            entry.Endpoint,
            entry.HttpMethod,
            entry.RequestQuery,
            entry.RequestIp,
            entry.StatusCode,
            IsSuccess = entry.IsSuccess ? 1 : 0,
            entry.DurationMs,
            entry.ResponseSummary,
            entry.ErrorMessage
        });
    }
}
