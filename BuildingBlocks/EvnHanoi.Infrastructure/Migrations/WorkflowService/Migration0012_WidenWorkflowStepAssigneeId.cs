using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.WorkflowService;

/// <summary>
/// Migration 0012: Nới cột ASSIGNEE_ID trên WORKFLOWSTEPS từ VARCHAR2(500) lên VARCHAR2(2000)
/// để chứa được danh sách nhiều ID người dùng (CSV), khớp độ rộng với SYSTEM_PERMISSION_GROUP_IDS /
/// UNIT_PERMISSION_GROUP_IDS. "Người cụ thể" (trước đây gọi "Giao việc đích danh") giờ cho phép
/// cấu hình nhiều người thay vì chỉ 1.
/// </summary>
public class Migration0012_WidenWorkflowStepAssigneeId : IScript
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
                throw new Exception($"[Migration0012-WF] SQL:\n{sql}\n{ex.Message}", ex);
            }
        }

        Exec("ALTER TABLE WORKFLOWSTEPS MODIFY ASSIGNEE_ID VARCHAR2(2000)");

        return string.Empty;
    }
}
