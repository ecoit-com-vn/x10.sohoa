using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.WorkflowService;

/// <summary>
/// Migration 0010: Chuẩn hóa liên kết quy trình qua cột WORKFLOW_TYPE_ID kiểu NUMBER.
/// Tạo bảng WORKFLOW_TYPES và seed 3 loại quy trình mặc định, cập nhật khóa ngoại và drop cột ENTITYTYPE cũ.
/// </summary>
public class Migration0010_WorkflowTypeTablesAndRelationships : IScript
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
                throw new Exception($"[Migration0010-WF] SQL:\n{sql}\n{ex.Message}", ex);
            }
        }

        // 1. Tạo bảng WORKFLOW_TYPES
        Exec(@"CREATE TABLE WORKFLOW_TYPES (
            ID NUMBER PRIMARY KEY,
            CODE VARCHAR2(50) NOT NULL UNIQUE,
            NAME VARCHAR2(255) NOT NULL,
            DESCRIPTION VARCHAR2(500),
            IS_ACTIVE NUMBER(1) DEFAULT 1
        )", 955); // ORA-00955: object name already exists

        // Seed 3 loại quy trình
        Exec("INSERT INTO WORKFLOW_TYPES (ID, CODE, NAME, DESCRIPTION) VALUES (1, 'Dossier', 'Quy trình số hóa hồ sơ', 'Quy trình số hóa hồ sơ thông thường')", 1); // ORA-00001: unique constraint violated
        Exec("INSERT INTO WORKFLOW_TYPES (ID, CODE, NAME, DESCRIPTION) VALUES (2, 'BorrowRecord', 'Quy trình mượn/trả hồ sơ kỹ thuật', 'Quy trình đăng ký mượn và trả hồ sơ kỹ thuật')", 1);
        Exec("INSERT INTO WORKFLOW_TYPES (ID, CODE, NAME, DESCRIPTION) VALUES (3, 'DossierDigitization', 'Quy trình số hóa hồ sơ (Digitization)', 'Quy trình số hóa hồ sơ có bước kiểm tra nhập liệu')", 1);

        // 2. Thêm cột WORKFLOW_TYPE_ID vào WORKFLOWDEFINITIONS và WORKFLOWINSTANCES
        Exec("ALTER TABLE WORKFLOWDEFINITIONS ADD WORKFLOW_TYPE_ID NUMBER", 1430); // ORA-01430: column being added already exists
        Exec("ALTER TABLE WORKFLOWINSTANCES ADD WORKFLOW_TYPE_ID NUMBER", 1430);

        // 3. Cập nhật dữ liệu từ cột ENTITYTYPE cũ sang WORKFLOW_TYPE_ID mới
        Exec("UPDATE WORKFLOWDEFINITIONS SET WORKFLOW_TYPE_ID = 1 WHERE ENTITYTYPE = 'Dossier'");
        Exec("UPDATE WORKFLOWDEFINITIONS SET WORKFLOW_TYPE_ID = 2 WHERE ENTITYTYPE = 'BorrowRecord'");
        Exec("UPDATE WORKFLOWDEFINITIONS SET WORKFLOW_TYPE_ID = 3 WHERE ENTITYTYPE = 'DossierDigitization'");

        Exec("UPDATE WORKFLOWINSTANCES SET WORKFLOW_TYPE_ID = 1 WHERE ENTITYTYPE = 'Dossier'");
        Exec("UPDATE WORKFLOWINSTANCES SET WORKFLOW_TYPE_ID = 2 WHERE ENTITYTYPE = 'BorrowRecord'");
        Exec("UPDATE WORKFLOWINSTANCES SET WORKFLOW_TYPE_ID = 3 WHERE ENTITYTYPE = 'DossierDigitization'");

        // Đảm bảo không null
        Exec("UPDATE WORKFLOWDEFINITIONS SET WORKFLOW_TYPE_ID = 1 WHERE WORKFLOW_TYPE_ID IS NULL");
        Exec("UPDATE WORKFLOWINSTANCES SET WORKFLOW_TYPE_ID = 1 WHERE WORKFLOW_TYPE_ID IS NULL");

        Exec("ALTER TABLE WORKFLOWDEFINITIONS MODIFY WORKFLOW_TYPE_ID NUMBER NOT NULL", 1442); // ORA-01442: column to be modified to NOT NULL is already NOT NULL
        Exec("ALTER TABLE WORKFLOWINSTANCES MODIFY WORKFLOW_TYPE_ID NUMBER NOT NULL", 1442);

        // 4. Drop các index cũ dựa trên cột ENTITYTYPE và tạo index mới
        Exec("DROP INDEX IX_WFDEF_ENTITYTYPE_ACTIVE", 1418); // ORA-01418: index does not exist
        Exec("CREATE INDEX IX_WFDEF_TYPE_ACTIVE ON WORKFLOWDEFINITIONS (WORKFLOW_TYPE_ID, ISACTIVE)", 955);

        Exec("DROP INDEX IX_WFINST_TARGET", 1418);
        Exec("CREATE INDEX IX_WFINST_TARGET ON WORKFLOWINSTANCES (TARGETENTITYID, WORKFLOW_TYPE_ID)", 955);

        // 5. Drop các cột ENTITYTYPE cũ lỏng lẻo
        Exec("ALTER TABLE WORKFLOWDEFINITIONS DROP COLUMN ENTITYTYPE", 904); // ORA-00904: invalid identifier (đã drop rồi)
        Exec("ALTER TABLE WORKFLOWINSTANCES DROP COLUMN ENTITYTYPE", 904);

        return string.Empty;
    }
}
