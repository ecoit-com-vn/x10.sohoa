using System.Data;
using Dapper;
using EvnHanoi.IdentityService.Core.Domain.Models;
using EvnHanoi.IdentityService.Core.Interfaces;

namespace EvnHanoi.IdentityService.Infrastructure.Repositories;

public class ExternalApiCallLogRepository : IExternalApiCallLogRepository
{
    private readonly IDbConnection _connection;

    public ExternalApiCallLogRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<(IEnumerable<ExternalApiCallLog> Items, int TotalCount)> GetByApiKeyIdAsync(long apiKeyId, int page, int pageSize)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var parameters = new DynamicParameters();
        parameters.Add("ApiKeyId", apiKeyId);

        const string countSql = "SELECT COUNT(1) FROM EXTERNAL_API_CALL_LOGS WHERE API_KEY_ID = :ApiKeyId";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        parameters.Add("Skip", (page - 1) * pageSize);
        parameters.Add("Take", pageSize);

        var sql = $@"
            SELECT
                ID AS {nameof(ExternalApiCallLog.Id)},
                API_KEY_ID AS {nameof(ExternalApiCallLog.ApiKeyId)},
                KEY_NAME AS {nameof(ExternalApiCallLog.KeyName)},
                ENDPOINT AS {nameof(ExternalApiCallLog.Endpoint)},
                HTTP_METHOD AS {nameof(ExternalApiCallLog.HttpMethod)},
                REQUEST_QUERY AS {nameof(ExternalApiCallLog.RequestQuery)},
                REQUEST_IP AS {nameof(ExternalApiCallLog.RequestIp)},
                STATUS_CODE AS {nameof(ExternalApiCallLog.StatusCode)},
                IS_SUCCESS AS {nameof(ExternalApiCallLog.IsSuccess)},
                DURATION_MS AS {nameof(ExternalApiCallLog.DurationMs)},
                RESPONSE_SUMMARY AS {nameof(ExternalApiCallLog.ResponseSummary)},
                ERROR_MESSAGE AS {nameof(ExternalApiCallLog.ErrorMessage)},
                CREATED_AT AS {nameof(ExternalApiCallLog.CreatedAt)}
            FROM EXTERNAL_API_CALL_LOGS
            WHERE API_KEY_ID = :ApiKeyId
            ORDER BY CREATED_AT DESC, ID DESC
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY";

        var items = await _connection.QueryAsync<ExternalApiCallLog>(sql, parameters);
        return (items, totalCount);
    }

    public async Task<(IEnumerable<ExternalApiCallLog> Items, int TotalCount)> GetAllAsync(ExternalApiCallLogFilter filter, int page, int pageSize)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        var parameters = new DynamicParameters();
        var whereClause = "";

        if (!string.IsNullOrWhiteSpace(filter.KeyName))
        {
            whereClause += " AND UPPER(COALESCE(l.KEY_NAME, k.KEY_NAME)) LIKE UPPER(:KeyName)";
            parameters.Add("KeyName", $"%{filter.KeyName.Trim()}%");
        }

        if (filter.DateFrom.HasValue)
        {
            whereClause += " AND l.CREATED_AT >= :DateFrom";
            parameters.Add("DateFrom", filter.DateFrom.Value);
        }

        if (filter.DateTo.HasValue)
        {
            whereClause += " AND l.CREATED_AT <= :DateTo";
            parameters.Add("DateTo", filter.DateTo.Value);
        }

        var baseFrom = $@"
            FROM EXTERNAL_API_CALL_LOGS l
            LEFT JOIN EXTERNAL_API_KEYS k ON k.ID = l.API_KEY_ID
            WHERE 1 = 1{whereClause}";

        var countSql = $"SELECT COUNT(1) {baseFrom}";
        var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);

        parameters.Add("Skip", (page - 1) * pageSize);
        parameters.Add("Take", pageSize);

        var sql = $@"
            SELECT
                l.ID AS {nameof(ExternalApiCallLog.Id)},
                l.API_KEY_ID AS {nameof(ExternalApiCallLog.ApiKeyId)},
                COALESCE(l.KEY_NAME, k.KEY_NAME) AS {nameof(ExternalApiCallLog.KeyName)},
                l.ENDPOINT AS {nameof(ExternalApiCallLog.Endpoint)},
                l.HTTP_METHOD AS {nameof(ExternalApiCallLog.HttpMethod)},
                l.REQUEST_QUERY AS {nameof(ExternalApiCallLog.RequestQuery)},
                l.REQUEST_IP AS {nameof(ExternalApiCallLog.RequestIp)},
                l.STATUS_CODE AS {nameof(ExternalApiCallLog.StatusCode)},
                l.IS_SUCCESS AS {nameof(ExternalApiCallLog.IsSuccess)},
                l.DURATION_MS AS {nameof(ExternalApiCallLog.DurationMs)},
                l.RESPONSE_SUMMARY AS {nameof(ExternalApiCallLog.ResponseSummary)},
                l.ERROR_MESSAGE AS {nameof(ExternalApiCallLog.ErrorMessage)},
                l.CREATED_AT AS {nameof(ExternalApiCallLog.CreatedAt)}
            {baseFrom}
            ORDER BY l.CREATED_AT DESC, l.ID DESC
            OFFSET :Skip ROWS FETCH NEXT :Take ROWS ONLY";

        var items = await _connection.QueryAsync<ExternalApiCallLog>(sql, parameters);
        return (items, totalCount);
    }
}
