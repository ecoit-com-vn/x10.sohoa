using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.WorkflowService;

/// <summary>
/// Migration 0011: Thêm cột IsDeleted vào bảng WORKFLOWDEFINITIONS để hỗ trợ xóa mềm.
/// </summary>
public class Migration0011_AddIsDeletedToWorkflowDefinitions : IScript
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
                throw new Exception($"[Migration0011-WF] SQL:\n{sql}\n{ex.Message}", ex);
            }
        }

        // 1. Thêm cột IsDeleted (PascalCase để thống nhất với các bảng khác)
        Exec("ALTER TABLE WORKFLOWDEFINITIONS ADD IsDeleted NUMBER(1) DEFAULT 0 NOT NULL", 1430); // ORA-01430: column being added already exists

        // 2. Tạo Index tối ưu
        Exec("CREATE INDEX IX_WFDEF_TYPE_ACTIVE_DELETED ON WORKFLOWDEFINITIONS (WORKFLOW_TYPE_ID, ISACTIVE, IsDeleted)", 955); // ORA-00955: object name already exists

        return string.Empty;
    }
}
