using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Bổ sung bảng tĩnh WORKFLOW_TASKS_ACTIVE lưu trữ trạng thái quy trình & available actions của hồ sơ.
/// </summary>
public class Migration0018_AddWorkflowTasksActiveTable : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        void ExecuteNonQuery(string sql, params int[] ignoreErrorCodes)
        {
            try
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                var ignored = false;
                foreach (var code in ignoreErrorCodes)
                {
                    if (ex.Message.Contains($"ORA-{code:D5}", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains($"ORA-0{code}", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains($"ORA-{code}", StringComparison.OrdinalIgnoreCase))
                    {
                        ignored = true;
                        break;
                    }
                }

                if (!ignored)
                    throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
            }
        }

        // ORA-00942 = table or view does not exist  → ignore on DROP
        // ORA-00955 = name already used             → ignore on CREATE
        // ORA-01430 = column being added already exists in table

        ExecuteNonQuery(@"
        CREATE TABLE WORKFLOW_TASKS_ACTIVE (
            ID                   VARCHAR2(36)  NOT NULL PRIMARY KEY,
            DOSSIER_ID            VARCHAR2(36)  NOT NULL,
            CURRENT_STEP_ID        VARCHAR2(100) NOT NULL,
            CURRENT_STEP_NAME      VARCHAR2(250) NOT NULL,
            CURRENT_ASSIGNEES     VARCHAR2(1000) NOT NULL,
            AVAILABLE_ACTIONS     VARCHAR2(2000) NOT NULL,
            CREATED_BY            VARCHAR2(100)  NULL,
            CREATED_DATE          TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
            LAST_MODIFIED_BY       VARCHAR2(100)  NULL,
            LAST_MODIFIED_DATE     TIMESTAMP      NULL,
            CONSTRAINT FK_WF_TASKS_DOSSIER FOREIGN KEY (DOSSIER_ID) REFERENCES DOSSIERS(Id) ON DELETE CASCADE
        )", 955);

        ExecuteNonQuery(@"
        CREATE INDEX IDX_WF_TASKS_DOSSIER ON WORKFLOW_TASKS_ACTIVE(DOSSIER_ID)
        ", 955);

        return string.Empty;
    }
}
