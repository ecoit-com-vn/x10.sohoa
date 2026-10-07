using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Hạ tầng cho job đồng bộ DANH SÁCH tài liệu PMIS riêng (PmisDocumentListSyncJob), thay cho pha tài liệu lồng trong lượt
/// Trạm/Đường dây/Thiết bị:
///  1. PMIS_SYNC_STATE thêm cột trạng thái tài liệu theo owner (OBJECT_TYPE='DOC_OWNER') và theo khoảng ngày (OBJECT_TYPE='DOC_WINDOW'):
///     REMOTE_TOTAL (tổng PMIS báo lần đếm gần nhất), LOCAL_COUNT (số tài liệu đã nhận), LAST_DOC_FETCH_AT (mốc lấy phần mới),
///     LAST_DOC_COUNT_AT (lần đếm gần nhất), LAST_DOC_FULL_AT (lần quét đầy đủ gần nhất), DOC_SCAN_SKIP (vị trí tiếp tục quét dở).
///  2. Nới CHECK OBJECT_TYPE của SYNC_CONFIG và SYNC_HISTORY thêm 'DOCUMENT'.
///  3. Seed dòng SYNC_CONFIG 'DOCUMENT' — TẮT sẵn (admin bật ở màn Thiết lập lịch sau khi kiểm tra), tần suất 2 giờ.
/// Idempotent (ORA-01430/ORA-02443/ORA-02264/ORA-00001 bỏ qua).
/// </summary>
public class Migration0017_AddDocumentListSync : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        // Chờ tối đa 60 giây nếu phiên khác (vd job đang UPDATE bảng) giữ khoá, thay vì thất bại ngay ORA-00054 làm pod crash-loop khi deploy.
        Execute(dbCommandFactory, "ALTER SESSION SET DDL_LOCK_TIMEOUT = 60", "ORA-00000");
        foreach (var column in new[]
        {
            "REMOTE_TOTAL NUMBER(10) NULL", "LOCAL_COUNT NUMBER(10) NULL", "LAST_DOC_FETCH_AT TIMESTAMP NULL",
            "LAST_DOC_COUNT_AT TIMESTAMP NULL", "LAST_DOC_FULL_AT TIMESTAMP NULL", "DOC_SCAN_SKIP NUMBER(10) NULL"
        })
        {
            Execute(dbCommandFactory, $"ALTER TABLE PMIS_SYNC_STATE ADD {column}", "ORA-01430");
        }

        Execute(dbCommandFactory, "ALTER TABLE SYNC_CONFIG DROP CONSTRAINT CK_SYNC_CONFIG_OBJECT_TYPE", "ORA-02443");
        Execute(dbCommandFactory,
            "ALTER TABLE SYNC_CONFIG ADD CONSTRAINT CK_SYNC_CONFIG_OBJECT_TYPE CHECK (OBJECT_TYPE IN ('SUBSTATION', 'TRANSMISSION_LINE', 'EQUIPMENT', 'DOCUMENT'))",
            "ORA-02264");
        Execute(dbCommandFactory, "ALTER TABLE SYNC_HISTORY DROP CONSTRAINT CK_SYNC_HISTORY_OBJECT_TYPE", "ORA-02443");
        Execute(dbCommandFactory,
            "ALTER TABLE SYNC_HISTORY ADD CONSTRAINT CK_SYNC_HISTORY_OBJECT_TYPE CHECK (OBJECT_TYPE IN ('SUBSTATION', 'TRANSMISSION_LINE', 'EQUIPMENT', 'DOCUMENT'))",
            "ORA-02264");

        using (var insert = dbCommandFactory())
        {
            insert.CommandText = @"
                INSERT INTO SYNC_CONFIG (ID, OBJECT_TYPE, FREQUENCY_VALUE, FREQUENCY_UNIT, IS_ENABLED)
                VALUES (:Id, 'DOCUMENT', 2, 'HOUR', 0)";
            var p = insert.CreateParameter();
            p.ParameterName = "Id";
            p.Value = Guid.CreateVersion7().ToString();
            insert.Parameters.Add(p);
            try
            {
                insert.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase))
            {
                // Dòng DOCUMENT đã tồn tại (chạy lại migration thủ công) — bỏ qua.
            }
        }

        return string.Empty;
    }

    private static void Execute(Func<IDbCommand> dbCommandFactory, string sql, string ignoreOraCode)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains(ignoreOraCode, StringComparison.OrdinalIgnoreCase))
        {
            // Đã ở đúng trạng thái mong muốn (chạy lại migration thủ công) — bỏ qua.
        }
    }
}
