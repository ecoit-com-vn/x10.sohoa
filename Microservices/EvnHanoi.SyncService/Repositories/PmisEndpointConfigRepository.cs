using System.Data;
using Dapper;
using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Repositories;

public class PmisEndpointConfigRepository : IPmisEndpointConfigRepository
{
    private readonly IDbConnection _connection;

    public PmisEndpointConfigRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<PmisApiEndpointConfigListItemDto>> GetAllAsync()
    {
        EnsureOpen();

        const string sql = @"
            SELECT c.ID AS Id,
                   c.API_CODE AS ApiCode,
                   c.DISPLAY_NAME AS DisplayName,
                   c.URL AS Url,
                   c.HTTP_METHOD AS HttpMethod,
                   c.TIMEOUT_SECONDS AS TimeoutSeconds,
                   c.IS_ACTIVE AS IsActive,
                   c.ROW_VERSION AS RowVersion,
                   (SELECT COUNT(1) FROM PMIS_API_ENDPOINT_HEADER h
                     WHERE h.ENDPOINT_CONFIG_ID = c.ID AND h.IS_DELETED = 0) AS HeaderCount
            FROM PMIS_API_ENDPOINT_CONFIG c
            WHERE c.IS_DELETED = 0
            ORDER BY c.API_CODE";
        return await _connection.QueryAsync<PmisApiEndpointConfigListItemDto>(sql);
    }

    public async Task<PmisApiEndpointConfig?> GetByApiCodeAsync(string apiCode)
    {
        EnsureOpen();

        const string sql = @"
            SELECT ID AS Id,
                   API_CODE AS ApiCode,
                   DISPLAY_NAME AS DisplayName,
                   URL AS Url,
                   HTTP_METHOD AS HttpMethod,
                   TIMEOUT_SECONDS AS TimeoutSeconds,
                   IS_ACTIVE AS IsActive,
                   ROW_VERSION AS RowVersion,
                   CREATED_BY AS CreatedBy,
                   CREATED_DATE AS CreatedDate,
                   MODIFIED_BY AS ModifiedBy,
                   MODIFIED_DATE AS ModifiedDate
            FROM PMIS_API_ENDPOINT_CONFIG
            WHERE API_CODE = :ApiCode AND IS_DELETED = 0";
        return await _connection.QuerySingleOrDefaultAsync<PmisApiEndpointConfig>(sql, new { ApiCode = apiCode });
    }

    public async Task<bool> UpdateAsync(string apiCode, UpdatePmisApiEndpointConfigRequest request, string? modifiedBy)
    {
        EnsureOpen();

        const string sql = @"
            UPDATE PMIS_API_ENDPOINT_CONFIG
            SET URL = :Url,
                TIMEOUT_SECONDS = :TimeoutSeconds,
                IS_ACTIVE = :IsActive,
                ROW_VERSION = ROW_VERSION + 1,
                MODIFIED_BY = :ModifiedBy,
                MODIFIED_DATE = SYSTIMESTAMP
            WHERE API_CODE = :ApiCode AND ROW_VERSION = :ExpectedVersion AND IS_DELETED = 0";

        var affected = await _connection.ExecuteAsync(sql, new
        {
            ApiCode = apiCode,
            request.Url,
            request.TimeoutSeconds,
            IsActive = request.IsActive ? 1 : 0,
            ExpectedVersion = request.RowVersion,
            ModifiedBy = modifiedBy
        });
        return affected > 0;
    }

    public async Task<IEnumerable<PmisApiEndpointHeader>> GetHeadersAsync(string endpointConfigId)
    {
        EnsureOpen();

        const string sql = @"
            SELECT ID AS Id,
                   ENDPOINT_CONFIG_ID AS EndpointConfigId,
                   HEADER_KEY AS HeaderKey,
                   HEADER_VALUE AS HeaderValue,
                   IS_SECRET AS IsSecret
            FROM PMIS_API_ENDPOINT_HEADER
            WHERE ENDPOINT_CONFIG_ID = :EndpointConfigId AND IS_DELETED = 0
            ORDER BY CREATED_DATE";
        return await _connection.QueryAsync<PmisApiEndpointHeader>(sql, new { EndpointConfigId = endpointConfigId });
    }

    public async Task ReplaceHeadersAsync(string endpointConfigId, IReadOnlyCollection<PmisApiEndpointHeader> headers, string? modifiedBy)
    {
        EnsureOpen();

        using var transaction = _connection.BeginTransaction();
        try
        {
            await _connection.ExecuteAsync(
                "DELETE FROM PMIS_API_ENDPOINT_HEADER WHERE ENDPOINT_CONFIG_ID = :EndpointConfigId",
                new { EndpointConfigId = endpointConfigId },
                transaction);

            const string insertSql = @"
                INSERT INTO PMIS_API_ENDPOINT_HEADER (
                    ID, ENDPOINT_CONFIG_ID, HEADER_KEY, HEADER_VALUE, IS_SECRET, CREATED_BY
                ) VALUES (
                    :Id, :EndpointConfigId, :HeaderKey, :HeaderValue, :IsSecret, :CreatedBy
                )";

            foreach (var header in headers)
            {
                await _connection.ExecuteAsync(insertSql, new
                {
                    Id = string.IsNullOrWhiteSpace(header.Id) ? Guid.CreateVersion7().ToString() : header.Id,
                    EndpointConfigId = endpointConfigId,
                    header.HeaderKey,
                    header.HeaderValue,
                    IsSecret = header.IsSecret ? 1 : 0,
                    CreatedBy = modifiedBy
                }, transaction);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }
}
