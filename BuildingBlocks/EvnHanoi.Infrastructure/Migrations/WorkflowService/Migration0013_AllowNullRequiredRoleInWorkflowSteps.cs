using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.WorkflowService;

/// <summary>
/// Migration 0013: Cho phép NULL trên cột REQUIREDROLE của WORKFLOWSTEPS.
/// Từ khi chuyển sang mô hình phân quyền theo nhóm quyền hệ thống/đơn vị + người cụ thể
/// (SYSTEM_PERMISSION_GROUP_IDS / UNIT_PERMISSION_GROUP_IDS / ASSIGNEE_ID), FE không còn
/// nhập RequiredRole nữa nên giá trị luôn rỗng — cột NOT NULL cũ gây ORA-01400 khi lưu/sửa
/// quy trình có bước không cấu hình role cụ thể.
/// </summary>
public class Migration0013_AllowNullRequiredRoleInWorkflowSteps : IScript
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
                throw new Exception($"[Migration0013-WF] SQL:\n{sql}\n{ex.Message}", ex);
            }
        }

        Exec("ALTER TABLE WORKFLOWSTEPS MODIFY REQUIREDROLE NULL");

        return string.Empty;
    }
}
