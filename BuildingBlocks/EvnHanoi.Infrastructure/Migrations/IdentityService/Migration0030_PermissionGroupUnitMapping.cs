using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>
/// Nhóm quyền đơn vị gắn nhiều đơn vị qua bảng mapping PERMISSION_GROUP_UNIT.
/// </summary>
public class Migration0030_PermissionGroupUnitMapping : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            void ExecuteDDL(string sql, params int[] ignoreErrorCodes)
            {
                try
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    bool ignored = false;
                    foreach (var code in ignoreErrorCodes)
                    {
                        if (ex.Message.Contains($"ORA-{code:D5}") || ex.Message.Contains($"ORA-0{code}") || ex.Message.Contains($"ORA-{code}"))
                        {
                            ignored = true;
                            break;
                        }
                    }
                    if (!ignored)
                    {
                        throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
                    }
                }
            }

            // 1. Mapping nhiều đơn vị cho một nhóm quyền
            ExecuteDDL(@"
                CREATE TABLE PERMISSION_GROUP_UNIT (
                    PermissionGroupId NUMBER NOT NULL,
                    OrganizationUnitId NUMBER NOT NULL,
                    PRIMARY KEY (PermissionGroupId, OrganizationUnitId),
                    CONSTRAINT fk_pgu_pg FOREIGN KEY (PermissionGroupId) REFERENCES PERMISSION_GROUP(Id) ON DELETE CASCADE,
                    CONSTRAINT fk_pgu_ou FOREIGN KEY (OrganizationUnitId) REFERENCES ORGANIZATION_UNIT(Id)
                )", 955);

            ExecuteDDL("CREATE INDEX idx_pgu_pg ON PERMISSION_GROUP_UNIT(PermissionGroupId)", 955);
            ExecuteDDL("CREATE INDEX idx_pgu_ou ON PERMISSION_GROUP_UNIT(OrganizationUnitId)", 955);

            // 2. Migrate dữ liệu cũ (1 đơn vị trên PERMISSION_GROUP)
            ExecuteDDL(@"
                INSERT INTO PERMISSION_GROUP_UNIT (PermissionGroupId, OrganizationUnitId)
                SELECT pg.Id, pg.OrganizationUnitId
                FROM PERMISSION_GROUP pg
                WHERE pg.OrganizationUnitId IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM PERMISSION_GROUP_UNIT x
                      WHERE x.PermissionGroupId = pg.Id AND x.OrganizationUnitId = pg.OrganizationUnitId
                  )");

            // 3. Nới CHECK: UNIT không bắt buộc OrganizationUnitId (dùng mapping)
            ExecuteDDL("ALTER TABLE PERMISSION_GROUP DROP CONSTRAINT chk_pg_scope", 2443);
            ExecuteDDL(@"
                ALTER TABLE PERMISSION_GROUP ADD CONSTRAINT chk_pg_scope CHECK (
                    (ScopeTypeId = 1 AND OrganizationUnitId IS NULL)
                    OR ScopeTypeId = 2
                )", 2264);
        }

        return string.Empty;
    }
}
