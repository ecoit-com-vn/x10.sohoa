using System.Data;
using Dapper;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;

namespace EvnHanoi.EquipmentService.Infrastructure.Repositories;

public class EquipmentPmisSpecRepository : IEquipmentPmisSpecRepository
{
    private readonly IDbConnection _connection;

    public EquipmentPmisSpecRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task UpsertAsync(Guid equipmentId, string? formValuesJson, string? syncHistoryId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var existingId = await _connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT Id FROM EQUIPMENT_PMIS_SPEC WHERE EquipmentId = :EquipmentId",
            new { EquipmentId = equipmentId.ToString() });

        if (existingId != null)
        {
            await _connection.ExecuteAsync(@"
                UPDATE EQUIPMENT_PMIS_SPEC
                SET FormValues = :FormValues, SyncedAt = SYSTIMESTAMP, SyncHistoryId = :SyncHistoryId,
                    RowVersion = RowVersion + 1, ModifiedDate = SYSTIMESTAMP
                WHERE Id = :Id",
                new { Id = existingId, FormValues = OracleClob.Param(formValuesJson), SyncHistoryId = syncHistoryId });
            return;
        }

        await _connection.ExecuteAsync(@"
            INSERT INTO EQUIPMENT_PMIS_SPEC (Id, EquipmentId, FormValues, SyncedAt, SyncHistoryId, CreatedBy)
            VALUES (:Id, :EquipmentId, :FormValues, SYSTIMESTAMP, :SyncHistoryId, :CreatedBy)",
            new
            {
                Id = Guid.CreateVersion7().ToString(),
                EquipmentId = equipmentId.ToString(),
                FormValues = OracleClob.Param(formValuesJson),
                SyncHistoryId = syncHistoryId,
                CreatedBy = "PMIS_SYNC"
            });
    }

    public async Task<(string? FormValues, DateTime? SyncedAt)?> GetByEquipmentIdAsync(Guid equipmentId)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        var row = await _connection.QuerySingleOrDefaultAsync(
            "SELECT FormValues, SyncedAt FROM EQUIPMENT_PMIS_SPEC WHERE EquipmentId = :EquipmentId",
            new { EquipmentId = equipmentId.ToString() });

        if (row == null) return null;
        return ((string?)row.FORMVALUES, (DateTime?)row.SYNCEDAT);
    }
}
