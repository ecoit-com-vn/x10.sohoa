using System.Data;
using Dapper;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.NotificationService.Repositories;

/// <summary>Cộng dồn lượt tra cứu theo ngày (LOOKUP_VIEW_DAILY_COUNTS) — MERGE upsert, không insert dòng mới mỗi lượt xem.</summary>
public class LookupTrackingRepository : ILookupTrackingRepository
{
    private readonly string _connectionString;

    public LookupTrackingRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection chưa được cấu hình cho NotificationService.");
    }

    public async Task RecordViewAsync(string entityType, string dossierId)
    {
        const string sql = """
            MERGE INTO LOOKUP_VIEW_DAILY_COUNTS t
            USING (SELECT :DossierId AS DossierId, :EntityType AS EntityType, TRUNC(SYSDATE) AS ViewDate FROM DUAL) s
            ON (t.DOSSIER_ID = s.DossierId AND t.ENTITY_TYPE = s.EntityType AND t.VIEW_DATE = s.ViewDate)
            WHEN MATCHED THEN
                UPDATE SET t.VIEW_COUNT = t.VIEW_COUNT + 1, t.MODIFIED_DATE = SYSTIMESTAMP
            WHEN NOT MATCHED THEN
                INSERT (DOSSIER_ID, ENTITY_TYPE, VIEW_DATE, VIEW_COUNT, CREATED_DATE)
                VALUES (s.DossierId, s.EntityType, s.ViewDate, 1, SYSTIMESTAMP)
            """;

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql, new { DossierId = dossierId, EntityType = entityType });
    }
}
