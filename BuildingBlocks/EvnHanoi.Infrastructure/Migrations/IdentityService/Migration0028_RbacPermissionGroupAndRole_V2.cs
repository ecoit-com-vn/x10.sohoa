using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

public class Migration0028_RbacPermissionGroupAndRole_V2 : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            // Helper method to execute DDL/DML and ignore specific Oracle error codes
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

            // 1. Create SCOPE_TYPE enum category table
            // Ignore ORA-00955 (name is already used by an existing object)
            ExecuteDDL(@"
                CREATE TABLE SCOPE_TYPE (
                    Id NUMBER PRIMARY KEY,
                    Code VARCHAR2(50) NOT NULL,
                    Name NVARCHAR2(255) NOT NULL,
                    CONSTRAINT uq_scope_type_code UNIQUE (Code)
                )", 955);

            ExecuteDDL(@"
                INSERT INTO SCOPE_TYPE (Id, Code, Name)
                SELECT 1, 'GLOBAL', 'Toàn hệ thống' FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM SCOPE_TYPE WHERE Id = 1)");

            ExecuteDDL(@"
                INSERT INTO SCOPE_TYPE (Id, Code, Name)
                SELECT 2, 'UNIT', 'Đơn vị' FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM SCOPE_TYPE WHERE Id = 2)");

            // 2. Drop FKs pointing to legacy ROLE
            // Ignore ORA-02443 (Cannot drop constraint - nonexistent constraint)
            // Ignore ORA-00942 (table or view does not exist - table already renamed)
            ExecuteDDL("ALTER TABLE USER_ROLE DROP CONSTRAINT fk_userrole_role", 2443, 942);
            ExecuteDDL("ALTER TABLE USER_GROUP_ROLE DROP CONSTRAINT fk_ugr_role", 2443, 942);
            ExecuteDDL("ALTER TABLE USER_UNIT_ROLE DROP CONSTRAINT fk_uur_role", 2443, 942);
            ExecuteDDL("ALTER TABLE ROLE_PERMISSION DROP CONSTRAINT fk_rp_role", 2443, 942);

            // 3. Rename legacy tables
            // Ignore ORA-00942 (table or view does not exist - table already renamed)
            // Ignore ORA-00955 (name is already used by an existing object)
            ExecuteDDL("ALTER TABLE ROLE RENAME TO PERMISSION_GROUP", 942, 955);
            ExecuteDDL("ALTER TABLE ROLE_PERMISSION RENAME TO PERMISSION_GROUP_PERMISSION", 942, 955);
            
            // Ignore ORA-00904 (invalid identifier - column already renamed)
            // Ignore ORA-00957 (duplicate column name - column already renamed)
            ExecuteDDL("ALTER TABLE PERMISSION_GROUP_PERMISSION RENAME COLUMN RoleId TO PermissionGroupId", 904, 957);

            // 4. Rebuild index on permission group permissions by renaming it
            // Ignore ORA-01418 (specified index does not exist)
            // Ignore ORA-00955 (name already used by existing index)
            ExecuteDDL("ALTER INDEX idx_rp_role_perm RENAME TO idx_pgp_pg_perm", 1418, 955);

            // Ignore ORA-02275 (such a referential constraint already exists)
            ExecuteDDL("ALTER TABLE PERMISSION_GROUP_PERMISSION ADD CONSTRAINT fk_pgp_pg FOREIGN KEY (PermissionGroupId) REFERENCES PERMISSION_GROUP(Id) ON DELETE CASCADE", 2275);

            // 5. Extend PERMISSION_GROUP with ScopeTypeId reference instead of string GroupType
            // Ignore ORA-01430 (column being added already exists in table)
            ExecuteDDL("ALTER TABLE PERMISSION_GROUP ADD ScopeTypeId NUMBER NULL", 1430);

            // Migrate data from legacy string ScopeType column
            // Ignore ORA-00904 if ScopeType column was already dropped
            try
            {
                ExecuteDDL("UPDATE PERMISSION_GROUP SET ScopeTypeId = 1 WHERE ScopeType = 'GLOBAL'", 904);
                ExecuteDDL("UPDATE PERMISSION_GROUP SET ScopeTypeId = 2 WHERE ScopeType = 'UNIT'", 904);
            }
            catch { }
            ExecuteDDL("UPDATE PERMISSION_GROUP SET ScopeTypeId = 1 WHERE ScopeTypeId IS NULL");

            // Set NOT NULL constraint on ScopeTypeId
            ExecuteDDL("ALTER TABLE PERMISSION_GROUP MODIFY ScopeTypeId NUMBER DEFAULT 1 NOT NULL", 1442);

            // Add OrganizationUnitId if not exists
            ExecuteDDL("ALTER TABLE PERMISSION_GROUP ADD OrganizationUnitId NUMBER NULL", 1430);

            // Ignore ORA-02275 (referential constraint already exists)
            ExecuteDDL("ALTER TABLE PERMISSION_GROUP ADD CONSTRAINT fk_pg_scope_type FOREIGN KEY (ScopeTypeId) REFERENCES SCOPE_TYPE(Id)", 2275);
            ExecuteDDL("ALTER TABLE PERMISSION_GROUP ADD CONSTRAINT fk_pg_orgunit FOREIGN KEY (OrganizationUnitId) REFERENCES ORGANIZATION_UNIT(Id)", 2275);

            // Ignore ORA-02264 (name already used by an existing constraint)
            ExecuteDDL("ALTER TABLE PERMISSION_GROUP ADD CONSTRAINT chk_pg_scope CHECK ((ScopeTypeId = 1 AND OrganizationUnitId IS NULL) OR (ScopeTypeId = 2 AND OrganizationUnitId IS NOT NULL))", 2264);

            // Drop legacy ScopeType column
            // Ignore ORA-00904 (column already dropped)
            ExecuteDDL("ALTER TABLE PERMISSION_GROUP DROP COLUMN ScopeType", 904);

            // 6. New ROLE (L3) with ScopeTypeId FK
            // Ignore ORA-00955 (name is already used by an existing object)
            ExecuteDDL(@"
                CREATE TABLE ROLE (
                    Id NUMBER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    Code VARCHAR2(50) NOT NULL,
                    Name VARCHAR2(255) NOT NULL,
                    Description VARCHAR2(1000) NULL,
                    ScopeTypeId NUMBER DEFAULT 1 NOT NULL,
                    OrganizationUnitId NUMBER NULL,
                    IsActive NUMBER(1) DEFAULT 1 NOT NULL,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    CreatedBy VARCHAR2(50),
                    UpdatedAt TIMESTAMP,
                    UpdatedBy VARCHAR2(50),
                    CONSTRAINT uq_role_l3_code UNIQUE (Code),
                    CONSTRAINT fk_role_l3_scope_type FOREIGN KEY (ScopeTypeId) REFERENCES SCOPE_TYPE(Id),
                    CONSTRAINT fk_role_l3_orgunit FOREIGN KEY (OrganizationUnitId) REFERENCES ORGANIZATION_UNIT(Id),
                    CONSTRAINT chk_role_l3_scope CHECK (
                        (ScopeTypeId = 1 AND OrganizationUnitId IS NULL) OR
                        (ScopeTypeId = 2 AND OrganizationUnitId IS NOT NULL)
                    )
                )", 955);

            // Ensure ROLE table has ScopeTypeId if it was created in a legacy run without it
            ExecuteDDL("ALTER TABLE ROLE ADD ScopeTypeId NUMBER NULL", 1430);
            try
            {
                ExecuteDDL("UPDATE ROLE SET ScopeTypeId = 1 WHERE ScopeType = 'GLOBAL'", 904);
                ExecuteDDL("UPDATE ROLE SET ScopeTypeId = 2 WHERE ScopeType = 'UNIT'", 904);
            }
            catch { }
            ExecuteDDL("UPDATE ROLE SET ScopeTypeId = 1 WHERE ScopeTypeId IS NULL");
            ExecuteDDL("ALTER TABLE ROLE MODIFY ScopeTypeId NUMBER DEFAULT 1 NOT NULL", 1442);
            ExecuteDDL("ALTER TABLE ROLE DROP CONSTRAINT chk_role_l3_scope", 2443);
            ExecuteDDL("ALTER TABLE ROLE DROP COLUMN ScopeType", 904);
            ExecuteDDL("ALTER TABLE ROLE ADD CONSTRAINT fk_role_l3_scope_type FOREIGN KEY (ScopeTypeId) REFERENCES SCOPE_TYPE(Id)", 2275);
            ExecuteDDL("ALTER TABLE ROLE ADD CONSTRAINT chk_role_l3_scope CHECK ((ScopeTypeId = 1 AND OrganizationUnitId IS NULL) OR (ScopeTypeId = 2 AND OrganizationUnitId IS NOT NULL))", 2264);

            ExecuteDDL(@"
                CREATE TABLE ROLE_PERMISSION_GROUP (
                    RoleId NUMBER NOT NULL,
                    PermissionGroupId NUMBER NOT NULL,
                    PRIMARY KEY (RoleId, PermissionGroupId),
                    CONSTRAINT fk_rpg_role FOREIGN KEY (RoleId) REFERENCES ROLE(Id) ON DELETE CASCADE,
                    CONSTRAINT fk_rpg_pg FOREIGN KEY (PermissionGroupId) REFERENCES PERMISSION_GROUP(Id) ON DELETE CASCADE
                )", 955);

            ExecuteDDL("CREATE INDEX idx_rpg_role ON ROLE_PERMISSION_GROUP(RoleId)", 955);
            ExecuteDDL("CREATE INDEX idx_rpg_pg ON ROLE_PERMISSION_GROUP(PermissionGroupId)", 955);

            // 7. Seed L3 roles from existing permission groups
            ExecuteDDL(@"
                INSERT INTO ROLE (Code, Name, Description, ScopeTypeId, OrganizationUnitId, IsActive, CreatedAt, CreatedBy)
                SELECT pg.Code, pg.Name, pg.Description, pg.ScopeTypeId, pg.OrganizationUnitId, pg.IsActive, pg.CreatedAt, pg.CreatedBy
                FROM PERMISSION_GROUP pg
                WHERE NOT EXISTS (SELECT 1 FROM ROLE r WHERE r.Code = pg.Code)");

            ExecuteDDL(@"
                INSERT INTO ROLE_PERMISSION_GROUP (RoleId, PermissionGroupId)
                SELECT r.Id, pg.Id
                FROM ROLE r
                INNER JOIN PERMISSION_GROUP pg ON r.Code = pg.Code
                WHERE NOT EXISTS (
                    SELECT 1 FROM ROLE_PERMISSION_GROUP x
                    WHERE x.RoleId = r.Id AND x.PermissionGroupId = pg.Id
                )");

            // 7. Remap USER_ROLE / USER_GROUP_ROLE / USER_UNIT_ROLE to L3 ROLE ids
            // Only update if RoleId currently references PERMISSION_GROUP.Id
            ExecuteDDL(@"
                UPDATE USER_ROLE ur
                SET RoleId = (
                    SELECT r.Id FROM ROLE r
                    INNER JOIN PERMISSION_GROUP pg ON r.Code = pg.Code
                    WHERE pg.Id = ur.RoleId AND ROWNUM = 1
                )
                WHERE EXISTS (
                    SELECT 1 FROM PERMISSION_GROUP pg 
                    INNER JOIN ROLE r ON r.Code = pg.Code
                    WHERE pg.Id = ur.RoleId
                )");

            ExecuteDDL(@"
                UPDATE USER_GROUP_ROLE ugr
                SET RoleId = (
                    SELECT r.Id FROM ROLE r
                    INNER JOIN PERMISSION_GROUP pg ON r.Code = pg.Code
                    WHERE pg.Id = ugr.RoleId AND ROWNUM = 1
                )
                WHERE EXISTS (
                    SELECT 1 FROM PERMISSION_GROUP pg 
                    INNER JOIN ROLE r ON r.Code = pg.Code
                    WHERE pg.Id = ugr.RoleId
                )");

            ExecuteDDL(@"
                UPDATE USER_UNIT_ROLE uur
                SET RoleId = (
                    SELECT r.Id FROM ROLE r
                    INNER JOIN PERMISSION_GROUP pg ON r.Code = pg.Code
                    WHERE pg.Id = uur.RoleId AND ROWNUM = 1
                )
                WHERE EXISTS (
                    SELECT 1 FROM PERMISSION_GROUP pg 
                    INNER JOIN ROLE r ON r.Code = pg.Code
                    WHERE pg.Id = uur.RoleId
                )");

            // 8. Restore FKs on assignment tables -> L3 ROLE
            // Ignore ORA-02275 (such a referential constraint already exists)
            ExecuteDDL("ALTER TABLE USER_ROLE ADD CONSTRAINT fk_userrole_role FOREIGN KEY (RoleId) REFERENCES ROLE(Id) ON DELETE CASCADE", 2275);
            ExecuteDDL("ALTER TABLE USER_GROUP_ROLE ADD CONSTRAINT fk_ugr_role FOREIGN KEY (RoleId) REFERENCES ROLE(Id) ON DELETE CASCADE", 2275);
            ExecuteDDL("ALTER TABLE USER_UNIT_ROLE ADD CONSTRAINT fk_uur_role FOREIGN KEY (RoleId) REFERENCES ROLE(Id) ON DELETE CASCADE", 2275);

            // 9. New permission codes for RBAC admin
            ExecuteDDL(@"
                INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
                SELECT 'perm-pg-view-uuid-000000000001', 'PERMISSION_GROUP_VIEW', N'Xem nhóm quyền hệ thống', N'Xem danh sách nhóm quyền hệ thống', 1, 'SYSTEM' FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'PERMISSION_GROUP_VIEW')");

            ExecuteDDL(@"
                INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
                SELECT 'perm-pg-manage-uuid-000000000002', 'PERMISSION_GROUP_MANAGE', N'Quản lý nhóm quyền hệ thống', N'Tạo/sửa/xóa nhóm quyền hệ thống và gắn quyền mịn', 1, 'SYSTEM' FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'PERMISSION_GROUP_MANAGE')");

            ExecuteDDL(@"
                INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
                SELECT 'perm-upg-view-uuid-000000000003', 'UNIT_PERMISSION_GROUP_VIEW', N'Xem nhóm quyền đơn vị', N'Xem danh sách nhóm quyền đơn vị', 1, 'SYSTEM' FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'UNIT_PERMISSION_GROUP_VIEW')");

            // Grant new permissions to ADMIN permission group
            // Ignore ORA-00942 if PERMISSION_GROUP_PERMISSION hasn't been created (fallback to ROLE_PERMISSION if not renamed yet)
            try
            {
                cmd.CommandText = @"
                    INSERT INTO PERMISSION_GROUP_PERMISSION (Id, PermissionGroupId, PermissionId)
                    SELECT SYS_GUID(), pg.Id, p.Id
                    FROM PERMISSION_GROUP pg
                    CROSS JOIN PERMISSION p
                    WHERE pg.Code = 'ADMIN'
                      AND p.Code IN ('PERMISSION_GROUP_VIEW', 'PERMISSION_GROUP_MANAGE', 'UNIT_PERMISSION_GROUP_VIEW')
                      AND NOT EXISTS (
                          SELECT 1 FROM PERMISSION_GROUP_PERMISSION x
                          WHERE x.PermissionGroupId = pg.Id AND x.PermissionId = p.Id
                      )";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ORA-00942"))
                {
                    // Fallback to legacy name if migration runs in a mixed state
                    cmd.CommandText = @"
                        INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionId)
                        SELECT SYS_GUID(), pg.Id, p.Id
                        FROM ROLE pg
                        CROSS JOIN PERMISSION p
                        WHERE pg.Code = 'ADMIN'
                          AND p.Code IN ('PERMISSION_GROUP_VIEW', 'PERMISSION_GROUP_MANAGE', 'UNIT_PERMISSION_GROUP_VIEW')
                          AND NOT EXISTS (
                              SELECT 1 FROM ROLE_PERMISSION x
                              WHERE x.RoleId = pg.Id AND x.PermissionId = p.Id
                          )";
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    throw;
                }
            }

            // 10. Menus: split role management
            ExecuteDDL(@"
                UPDATE APP_MENU
                SET Name = N'Nhóm quyền hệ thống',
                    Url = '/administration/system-permission-groups',
                    PermissionCode = 'PERMISSION_GROUP_VIEW'
                WHERE Url = '/administration/role-management'");

            ExecuteDDL(@"
                INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
                SELECT N'Nhóm quyền đơn vị', '/administration/unit-permission-groups', 'pi pi-building', 2, 3, 1, 'UNIT_PERMISSION_GROUP_VIEW' FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Url = '/administration/unit-permission-groups')");

            ExecuteDDL(@"
                INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
                SELECT N'Vai trò', '/administration/roles', 'pi pi-id-card', 2, 4, 1, 'ROLE_VIEW' FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Url = '/administration/roles')");
        }

        return string.Empty;
    }
}
