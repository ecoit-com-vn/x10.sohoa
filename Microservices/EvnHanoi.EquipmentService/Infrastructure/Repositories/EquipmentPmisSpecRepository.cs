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

        // MERGE atomic ở tầng DB — tránh race condition check-then-act (2 tiến trình đồng bộ cùng
        // 1 thiết bị đồng thời, ví dụ đồng bộ thủ công trùng lúc job tự động chạy, đều thấy "chưa có
        // dòng nào" rồi cùng INSERT, vi phạm UQ_EQUIPMENT_PMIS_SPEC_EQUIP).
        await _connection.ExecuteAsync(@"
            MERGE INTO EQUIPMENT_PMIS_SPEC target
            USING (SELECT :EquipmentId AS EquipmentId FROM DUAL) src
            ON (target.EquipmentId = src.EquipmentId)
            WHEN MATCHED THEN UPDATE SET
                target.FormValues = :FormValues,
                target.SyncedAt = SYSTIMESTAMP,
                target.SyncHistoryId = :SyncHistoryId,
                target.RowVersion = target.RowVersion + 1,
                target.ModifiedBy = :ModifiedBy,
                target.ModifiedDate = SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (Id, EquipmentId, FormValues, SyncedAt, SyncHistoryId, CreatedBy)
            VALUES (:Id, :EquipmentId, :FormValues, SYSTIMESTAMP, :SyncHistoryId, :ModifiedBy)",
            new
            {
                Id = Guid.CreateVersion7().ToString(),
                EquipmentId = equipmentId.ToString(),
                FormValues = OracleClob.Param(formValuesJson),
                SyncHistoryId = syncHistoryId,
                ModifiedBy = "PMIS_SYNC"
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

    public async Task<IEnumerable<string?>> GetRecentFormValuesByEquipmentTypeAsync(Guid equipmentTypeId, int maxRows)
    {
        if (_connection.State != ConnectionState.Open) _connection.Open();

        const string sql = @"
            SELECT FormValues FROM (
                SELECT s.FormValues AS FormValues
                FROM EQUIPMENT_PMIS_SPEC s
                JOIN EQUIPMENTS e ON e.Id = s.EquipmentId
                WHERE s.IsDeleted = 0 AND e.IsDeleted = 0 AND e.EquipmentTypeId = :EquipmentTypeId
                ORDER BY s.SyncedAt DESC
            ) WHERE ROWNUM <= :MaxRows";

        return await _connection.QueryAsync<string?>(sql, new
        {
            EquipmentTypeId = equipmentTypeId.ToString(),
            MaxRows = maxRows
        });
    }
}
