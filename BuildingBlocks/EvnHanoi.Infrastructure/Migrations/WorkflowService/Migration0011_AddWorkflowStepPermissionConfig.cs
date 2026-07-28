using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.WorkflowService;

/// <summary>
/// Migration 0011: Thêm các cột cấu hình nhóm quyền, cùng đơn vị và giao việc đích danh vào WORKFLOWSTEPS.
/// - SYSTEM_PERMISSION_GROUP_IDS: danh sách ID nhóm quyền hệ thống (phân cách bởi dấu phẩy)
/// - UNIT_PERMISSION_GROUP_IDS: danh sách ID nhóm quyền đơn vị (phân cách bởi dấu phẩy)
/// - REQUIRE_SAME_UNIT: bắt buộc người xử lý tiếp theo phải cùng đơn vị với người chuyển
/// - ASSIGNEE_ID: ID người dùng được giao việc đích danh (nếu có thì bỏ qua bước chọn người)
/// </summary>
public class Migration0011_AddWorkflowStepPermissionConfig : IScript
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

        // Thêm cột danh sách nhóm quyền hệ thống (CSV các ID, tối đa 2000 ký tự)
        Exec("ALTER TABLE WORKFLOWSTEPS ADD SYSTEM_PERMISSION_GROUP_IDS VARCHAR2(2000)", 1430); // ORA-01430: column already exists

        // Thêm cột danh sách nhóm quyền đơn vị
        Exec("ALTER TABLE WORKFLOWSTEPS ADD UNIT_PERMISSION_GROUP_IDS VARCHAR2(2000)", 1430);

        // Thêm cột cờ yêu cầu cùng đơn vị
        Exec("ALTER TABLE WORKFLOWSTEPS ADD REQUIRE_SAME_UNIT NUMBER(1) DEFAULT 0", 1430);

        // Thêm cột ID người dùng được giao việc đích danh
        Exec("ALTER TABLE WORKFLOWSTEPS ADD ASSIGNEE_ID VARCHAR2(500)", 1430);

        return string.Empty;
    }
}
