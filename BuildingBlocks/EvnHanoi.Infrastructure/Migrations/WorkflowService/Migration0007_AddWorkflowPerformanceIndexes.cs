using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.WorkflowService;

/// <summary>
/// Thêm các index cho bảng Workflow phục vụ tối ưu hiệu năng và tránh 504 Timeout:
/// 1. Index (TARGETENTITYID, TARGETENTITYTYPE) trên WORKFLOWINSTANCES để tối ưu query theo hồ sơ.
/// 2. Index WORKFLOWINSTANCEID trên WORKFLOWTASKS để tối ưu query danh sách task theo quy trình.
/// 3. Index WORKFLOWINSTANCEID trên WORKFLOWHISTORY để tối ưu query lịch sử quy trình.
/// ORA-00955 (name is already used by an existing object) được bỏ qua để migration idempotent.
/// </summary>
public class Migration0007_AddWorkflowPerformanceIndexes : IScript
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
                throw new Exception($"[Migration0007-WF] SQL:\n{sql}\n{ex.Message}", ex);
            }
        }

        // ORA-00955 = name is already used by an existing object
        Exec("CREATE INDEX IX_WFINST_TARGET ON WORKFLOWINSTANCES (TARGETENTITYID, TARGETENTITYTYPE)", 955);
        Exec("CREATE INDEX IX_WFTASK_WFINST ON WORKFLOWTASKS (WORKFLOWINSTANCEID)", 955);
        Exec("CREATE INDEX IX_WFHIST_WFINST ON WORKFLOWHISTORY (WORKFLOWINSTANCEID)", 955);

        return string.Empty;
    }
}
