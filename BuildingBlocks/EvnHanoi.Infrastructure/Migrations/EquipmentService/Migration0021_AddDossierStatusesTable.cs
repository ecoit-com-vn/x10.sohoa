using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0021_AddDossierStatusesTable : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            void ExecuteNonQuery(string sql, params int[] ignoreErrorCodes)
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

            // 1. Tạo bảng danh mục DOSSIER_STATUSES
            // ORA-00955: name is already used by an existing object
            ExecuteNonQuery(@"
                CREATE TABLE DOSSIER_STATUSES (
                    Id NUMBER PRIMARY KEY,
                    Code VARCHAR2(50) NOT NULL UNIQUE,
                    Name VARCHAR2(255) NOT NULL
                )", 955);

            // 2. Chèn dữ liệu tĩnh cho DOSSIER_STATUSES
            // ORA-00001: unique constraint (violation of primary key/unique key)
            ExecuteNonQuery("INSERT INTO DOSSIER_STATUSES (Id, Code, Name) VALUES (1, 'New', 'Tạo mới')", 1);
            ExecuteNonQuery("INSERT INTO DOSSIER_STATUSES (Id, Code, Name) VALUES (2, 'CompletedInput', 'Hoàn thành')", 1);
            ExecuteNonQuery("INSERT INTO DOSSIER_STATUSES (Id, Code, Name) VALUES (3, 'PendingApproval', 'Chờ duyệt')", 1);
            ExecuteNonQuery("INSERT INTO DOSSIER_STATUSES (Id, Code, Name) VALUES (4, 'InProgress', 'Đang xử lý')", 1);
            ExecuteNonQuery("INSERT INTO DOSSIER_STATUSES (Id, Code, Name) VALUES (5, 'Returned', 'Trả lại')", 1);
            ExecuteNonQuery("INSERT INTO DOSSIER_STATUSES (Id, Code, Name) VALUES (6, 'Approved', 'Đã duyệt')", 1);

            // 3. Thêm cột STATUS_ID vào bảng DOSSIERS
            // ORA-01430: column being added already exists in table
            ExecuteNonQuery("ALTER TABLE DOSSIERS ADD STATUS_ID NUMBER NULL", 1430);

            // 4. Cập nhật dữ liệu từ cột STATUS cũ sang STATUS_ID mới
            ExecuteNonQuery("UPDATE DOSSIERS SET STATUS_ID = 1 WHERE Status = 'New' AND STATUS_ID IS NULL");
            ExecuteNonQuery("UPDATE DOSSIERS SET STATUS_ID = 2 WHERE Status = 'CompletedInput' AND STATUS_ID IS NULL");
            ExecuteNonQuery("UPDATE DOSSIERS SET STATUS_ID = 3 WHERE Status = 'PendingApproval' AND STATUS_ID IS NULL");
            ExecuteNonQuery("UPDATE DOSSIERS SET STATUS_ID = 4 WHERE Status = 'InProgress' AND STATUS_ID IS NULL");
            ExecuteNonQuery("UPDATE DOSSIERS SET STATUS_ID = 5 WHERE Status = 'Returned' AND STATUS_ID IS NULL");
            ExecuteNonQuery("UPDATE DOSSIERS SET STATUS_ID = 6 WHERE Status = 'Approved' AND STATUS_ID IS NULL");
            
            // Xử lý các dòng có Status = 'Draft' hoặc giá trị khác chưa map
            ExecuteNonQuery("UPDATE DOSSIERS SET STATUS_ID = 1 WHERE STATUS_ID IS NULL");

            // 5. Thiết lập NOT NULL cho STATUS_ID sau khi đã điền đầy đủ dữ liệu
            // ORA-01442: column to be modified to NOT NULL is already NOT NULL
            ExecuteNonQuery("ALTER TABLE DOSSIERS MODIFY STATUS_ID NUMBER NOT NULL", 1442);

            // 6. Thêm ràng buộc khóa ngoại fk_dossier_status
            // ORA-02275: such a referential constraint already exists in the table
            // ORA-02264: name already used by an existing constraint
            ExecuteNonQuery("ALTER TABLE DOSSIERS ADD CONSTRAINT fk_dossier_status FOREIGN KEY (STATUS_ID) REFERENCES DOSSIER_STATUSES(Id)", 2275, 2264);

            // 7. Xóa cột STATUS cũ
            // ORA-00904: invalid identifier (nếu cột đã bị xóa từ trước)
            ExecuteNonQuery("ALTER TABLE DOSSIERS DROP COLUMN Status", 904);
        }

        return string.Empty;
    }
}
