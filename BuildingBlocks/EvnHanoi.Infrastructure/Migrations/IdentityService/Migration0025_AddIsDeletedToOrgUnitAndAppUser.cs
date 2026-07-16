using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>
/// Soft delete IsDeleted trên ORGANIZATION_UNIT / APP_USER (idempotent).
/// </summary>
public class Migration0025_AddIsDeletedToOrgUnitAndAppUser : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            void ExecuteIgnore(string sql, params int[] ignoreCodes)
            {
                try
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    foreach (var code in ignoreCodes)
                    {
                        if (ex.Message.Contains($"ORA-{code:D5}") || ex.Message.Contains($"ORA-{code}"))
                            return;
                    }
                    throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
                }
            }

            // ORA-01430: column already exists
            ExecuteIgnore("ALTER TABLE ORGANIZATION_UNIT ADD IsDeleted NUMBER(1) DEFAULT 0 NOT NULL", 1430);
            ExecuteIgnore("ALTER TABLE APP_USER ADD IsDeleted NUMBER(1) DEFAULT 0 NOT NULL", 1430);
            // ORA-00955: name already used
            ExecuteIgnore("CREATE INDEX IDX_ORG_UNIT_IS_DELETED ON ORGANIZATION_UNIT(IsDeleted)", 955);
            ExecuteIgnore("CREATE INDEX IDX_APP_USER_IS_DELETED ON APP_USER(IsDeleted)", 955);
        }

        return string.Empty;
    }
}
