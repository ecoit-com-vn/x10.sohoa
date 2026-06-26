using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.WorkflowService;

/// <summary>
/// Đổi WORKFLOWINSTANCES.TARGETENTITYTYPE → ENTITYTYPE (thống nhất với WorkflowDefinition.EntityType).
/// Tái tạo index tra cứu theo (TARGETENTITYID, ENTITYTYPE).
/// </summary>
public class Migration0009_RenameInstanceTargetEntityTypeToEntityType : IScript
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
                throw new Exception($"[Migration0009-WF] SQL:\n{sql}\n{ex.Message}", ex);
            }
        }

        // ORA-00957 = duplicate column name (đã rename rồi)
        Exec("ALTER TABLE WORKFLOWINSTANCES RENAME COLUMN TARGETENTITYTYPE TO ENTITYTYPE", 957, 904);

        Exec("DROP INDEX IX_WFINST_TARGET", 1418);
        Exec("CREATE INDEX IX_WFINST_TARGET ON WORKFLOWINSTANCES (TARGETENTITYID, ENTITYTYPE)", 955);

        return string.Empty;
    }
}
