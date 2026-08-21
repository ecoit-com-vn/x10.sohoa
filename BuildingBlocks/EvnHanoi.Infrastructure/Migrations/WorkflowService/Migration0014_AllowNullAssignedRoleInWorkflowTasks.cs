using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.WorkflowService;

/// <summary>
/// Migration 0014: Cho phép NULL trên cột ASSIGNEDROLE của WORKFLOWTASKS.
/// Cùng gốc vấn đề đã được Migration0013 xử lý cho WORKFLOWSTEPS (bước cấu hình bằng
/// SYSTEM_PERMISSION_GROUP_IDS / UNIT_PERMISSION_GROUP_IDS / ASSIGNEE_ID thay vì RequiredRole
/// khiến RequiredRole hợp lệ là NULL), nhưng khi đó chỉ sửa bảng cấu hình (WORKFLOWSTEPS),
/// chưa sửa bảng thực thi (WORKFLOWTASKS). WorkflowEngineService.SubmitInternalAsync gán thẳng
/// AssignedRole = firstStep.RequiredRole (nay hợp lệ là null) vào cột NOT NULL cũ, gây
/// ORA-01400 khi submit hồ sơ có bước đầu không cấu hình theo Vai trò.
/// </summary>
public class Migration0014_AllowNullAssignedRoleInWorkflowTasks : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        try
        {
            cmd.CommandText = "ALTER TABLE WORKFLOWTASKS MODIFY ASSIGNEDROLE NULL";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01451", StringComparison.OrdinalIgnoreCase))
        {
            // ORA-01451: column already nullable — migration đã chạy trước đó.
        }

        return string.Empty;
    }
}
