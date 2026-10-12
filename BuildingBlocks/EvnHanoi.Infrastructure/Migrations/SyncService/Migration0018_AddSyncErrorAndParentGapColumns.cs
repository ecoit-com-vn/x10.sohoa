using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Nền toàn vẹn cho đồng bộ PMIS (Đợt 1 của kế hoạch tối ưu 2026-10-09):
///  1. Bảng PMIS_SYNC_ERROR — mỗi bản ghi lỗi có ĐÚNG 1 dòng theo (ENTITY_KIND, PMIS_CODE, ERROR_CODE): số lần gặp, trạng thái
///     OPEN/QUARANTINED/RESOLVED, hạn thử lại và hash nguồn lúc lỗi. Lỗi do dữ liệu nguồn (vd. thiếu cấp điện áp) sau N lần liên tiếp bị CÁCH LY
///     — không thử lại mỗi lượt cho tới khi hạn thử lại tới hoặc hash nguồn đổi (PMIS đã sửa) hoặc admin bấm "thử lại".
///  2. PMIS_SYNC_STATE thêm QUARANTINED_COUNT, GAP_FLAG (đối chiếu số lượng theo cha: REMOTE_TOTAL − LOCAL_COUNT − QUARANTINED_COUNT &gt; 0) và
///     MISSING_STREAK (số đợt quét đầy đủ liên tiếp không thấy bản ghi trên PMIS — dùng cho phát hiện bản ghi biến mất).
/// Idempotent (ORA-00955/ORA-01430 bỏ qua). Không động tới dữ liệu cũ.
/// </summary>
public class Migration0018_AddSyncErrorAndParentGapColumns : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory, "ALTER SESSION SET DDL_LOCK_TIMEOUT = 60", "ORA-00000");

        Execute(dbCommandFactory, @"
            CREATE TABLE PMIS_SYNC_ERROR (
                ID              VARCHAR2(36)   NOT NULL,
                ENTITY_KIND     VARCHAR2(20)   NOT NULL,
                PMIS_CODE       VARCHAR2(150)  NOT NULL,
                ERROR_CODE      VARCHAR2(40)   NOT NULL,
                MESSAGE         VARCHAR2(1000) NULL,
                STATE           VARCHAR2(16)   DEFAULT 'OPEN' NOT NULL,
                OCCURRENCES     NUMBER(10)     DEFAULT 1 NOT NULL,
                FIRST_SEEN_AT   TIMESTAMP      NOT NULL,
                LAST_SEEN_AT    TIMESTAMP      NOT NULL,
                NEXT_RETRY_AT   TIMESTAMP      NULL,
                RESOLVED_AT     TIMESTAMP      NULL,
                SOURCE_HASH     VARCHAR2(64)   NULL,
                SYNC_HISTORY_ID VARCHAR2(36)   NULL,
                CONSTRAINT PK_PMIS_SYNC_ERROR PRIMARY KEY (ID),
                CONSTRAINT UQ_PMIS_SYNC_ERROR UNIQUE (ENTITY_KIND, PMIS_CODE, ERROR_CODE),
                CONSTRAINT CK_PMIS_SYNC_ERROR_STATE CHECK (STATE IN ('OPEN', 'QUARANTINED', 'RESOLVED'))
            )", "ORA-00955");
        Execute(dbCommandFactory, "CREATE INDEX IDX_PMIS_SYNC_ERROR_STATE ON PMIS_SYNC_ERROR (STATE, ERROR_CODE)", "ORA-00955");
        Execute(dbCommandFactory, "CREATE INDEX IDX_PMIS_SYNC_ERROR_CODE ON PMIS_SYNC_ERROR (ENTITY_KIND, PMIS_CODE)", "ORA-00955");

        foreach (var column in new[] { "QUARANTINED_COUNT NUMBER(10) NULL", "GAP_FLAG NUMBER(1) NULL", "MISSING_STREAK NUMBER(3) NULL" })
            Execute(dbCommandFactory, $"ALTER TABLE PMIS_SYNC_STATE ADD {column}", "ORA-01430");

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
