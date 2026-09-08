using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Models;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class DigitalSignatureEndpointConfigRepository : IDigitalSignatureEndpointConfigRepository
{
    private readonly IDbConnection _connection;

    public DigitalSignatureEndpointConfigRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<DigitalSignatureEndpointConfigListItemDto>> GetAllAsync()
    {
        EnsureOpen();

        const string sql = @"
            SELECT ID AS Id,
                   API_CODE AS ApiCode,
                   DISPLAY_NAME AS DisplayName,
                   URL AS Url,
                   IS_ACTIVE AS IsActive,
                   ROW_VERSION AS RowVersion
            FROM DIGITAL_SIGNATURE_ENDPOINT_CONFIG
            WHERE IS_DELETED = 0
            ORDER BY API_CODE";
        return await _connection.QueryAsync<DigitalSignatureEndpointConfigListItemDto>(sql);
    }

    public async Task<DigitalSignatureEndpointConfig?> GetByApiCodeAsync(string apiCode)
    {
        EnsureOpen();

        const string sql = @"
            SELECT ID AS Id,
                   API_CODE AS ApiCode,
                   DISPLAY_NAME AS DisplayName,
                   URL AS Url,
                   IS_ACTIVE AS IsActive,
                   ROW_VERSION AS RowVersion,
                   CREATED_BY AS CreatedBy,
                   CREATED_DATE AS CreatedDate,
                   MODIFIED_BY AS ModifiedBy,
                   MODIFIED_DATE AS ModifiedDate
            FROM DIGITAL_SIGNATURE_ENDPOINT_CONFIG
            WHERE API_CODE = :ApiCode AND IS_DELETED = 0";
        return await _connection.QuerySingleOrDefaultAsync<DigitalSignatureEndpointConfig>(sql, new { ApiCode = apiCode });
    }

    public async Task<bool> UpdateAsync(string apiCode, UpdateDigitalSignatureEndpointConfigRequest request, string? modifiedBy)
    {
        EnsureOpen();

        const string sql = @"
            UPDATE DIGITAL_SIGNATURE_ENDPOINT_CONFIG
            SET URL = :Url,
                IS_ACTIVE = :IsActive,
                ROW_VERSION = ROW_VERSION + 1,
                MODIFIED_BY = :ModifiedBy,
                MODIFIED_DATE = SYSTIMESTAMP
            WHERE API_CODE = :ApiCode AND ROW_VERSION = :ExpectedVersion AND IS_DELETED = 0";

        var affected = await _connection.ExecuteAsync(sql, new
        {
            ApiCode = apiCode,
            request.Url,
            IsActive = request.IsActive ? 1 : 0,
            ExpectedVersion = request.RowVersion,
            ModifiedBy = modifiedBy
        });
        return affected > 0;
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }
}
