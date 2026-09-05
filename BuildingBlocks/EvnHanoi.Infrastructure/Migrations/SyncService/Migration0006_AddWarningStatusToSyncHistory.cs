using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Nới CHECK constraint của SYNC_HISTORY/SYNC_HISTORY_DETAIL để chấp nhận thêm trạng thái 'WARNING' —
/// dùng cho các bước phụ có thể lỗi mà không làm hỏng cả lượt đồng bộ chính (vd. đồng bộ tài liệu đính
/// kèm — xem PmisSyncExecutionService.SyncDocumentsForOwnerAsync). Trước đây STATUS chỉ nhận
/// RUNNING/SUCCESS/FAILED (history) và SUCCESS/FAILED (detail).
/// </summary>
public class Migration0006_AddWarningStatusToSyncHistory : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory, "ALTER TABLE SYNC_HISTORY DROP CONSTRAINT CK_SYNC_HISTORY_STATUS", "ORA-02443");
        Execute(dbCommandFactory,
            "ALTER TABLE SYNC_HISTORY ADD CONSTRAINT CK_SYNC_HISTORY_STATUS CHECK (STATUS IN ('RUNNING', 'SUCCESS', 'FAILED', 'WARNING'))",
            "ORA-02264");

        Execute(dbCommandFactory, "ALTER TABLE SYNC_HISTORY_DETAIL DROP CONSTRAINT CK_SYNC_HISTORY_DETAIL_STATUS", "ORA-02443");
        Execute(dbCommandFactory,
            "ALTER TABLE SYNC_HISTORY_DETAIL ADD CONSTRAINT CK_SYNC_HISTORY_DETAIL_STATUS CHECK (STATUS IN ('SUCCESS', 'FAILED', 'WARNING'))",
            "ORA-02264");

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
            // Constraint đã ở đúng trạng thái mong muốn (chạy lại migration thủ công) — bỏ qua.
        }
    }
}
