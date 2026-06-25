using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.WorkflowService;

/// <summary>
/// 1. Chuẩn hóa WORKFLOWDEFINITIONS.EntityType từ tên hiển thị (legacy) sang Code (Dossier, BorrowRecord).
/// 2. Index (EntityType, IsActive) để tìm definition active nhanh hơn.
/// </summary>
public class Migration0008_WorkflowEntityTypeCodeAndIndex : IScript
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
                throw new Exception($"[Migration0008-WF] SQL:\n{sql}\n{ex.Message}", ex);
            }
        }

        Exec(@"UPDATE WORKFLOWDEFINITIONS SET EntityType = 'Dossier'
               WHERE EntityType = 'Quy trình số hóa hồ sơ'
                  OR Name = 'Quy trình số hóa hồ sơ'", 942);

        Exec(@"UPDATE WORKFLOWDEFINITIONS SET EntityType = 'BorrowRecord'
               WHERE EntityType = 'Quy trình mượn/trả hồ sơ kỹ thuật'
                  OR Name = 'Quy trình mượn/trả hồ sơ kỹ thuật'", 942);

        Exec("CREATE INDEX IX_WFDEF_ENTITYTYPE_ACTIVE ON WORKFLOWDEFINITIONS (EntityType, IsActive)", 955);

        return string.Empty;
    }
}
