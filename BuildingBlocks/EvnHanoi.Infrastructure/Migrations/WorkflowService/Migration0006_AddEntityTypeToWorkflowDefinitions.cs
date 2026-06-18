using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.WorkflowService;

/// <summary>
/// Thêm cột EntityType vào bảng WORKFLOWDEFINITIONS.
/// Dùng để WorkflowEngine tự tìm definition phù hợp theo loại entity
/// (ví dụ: "Dossier", "BorrowRecord") khi submit mà không cần truyền definitionId.
/// ORA-01430 (column already exists) được bỏ qua để migration idempotent.
/// </summary>
public class Migration0006_AddEntityTypeToWorkflowDefinitions : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        void Exec(string sql, params int[] ignoreOra)
        {
            try
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                foreach (var code in ignoreOra)
                {
                    if (ex.Message.Contains($"ORA-{code:D5}") ||
                        ex.Message.Contains($"ORA-0{code}") ||
                        ex.Message.Contains($"ORA-{code}"))
                        return;
                }
                throw new Exception($"[Migration0006-WF] SQL:\n{sql}\n{ex.Message}", ex);
            }
        }

        // ORA-01430 = column being added already exists in table
        Exec("ALTER TABLE WorkflowDefinitions ADD EntityType VARCHAR2(100) DEFAULT '' NOT NULL", 1430);

        return string.Empty;
    }
}
