using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0023_CreateFolderUserAllocationsTable : IScript
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

            // Tạo bảng FOLDER_USER_ALLOCATIONS
            // ORA-00955: name is already used by an existing object
            ExecuteNonQuery(@"
                CREATE TABLE FOLDER_USER_ALLOCATIONS (
                    ID            VARCHAR2(36) PRIMARY KEY,
                    FOLDER_ID     VARCHAR2(36) NOT NULL,
                    USER_ID       VARCHAR2(36) NOT NULL,
                    UNIT_ID       NUMBER NOT NULL,
                    STATUS        VARCHAR2(50) NOT NULL,
                    ROW_VERSION   NUMBER DEFAULT 1 NOT NULL,
                    CREATED_BY    VARCHAR2(100),
                    CREATED_DATE  TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL,
                    MODIFIED_BY   VARCHAR2(100),
                    MODIFIED_DATE TIMESTAMP,
                    IS_DELETED    NUMBER(1) DEFAULT 0 NOT NULL,
                    CONSTRAINT FK_FUA_FOLDER FOREIGN KEY (FOLDER_ID) REFERENCES FOLDERS(ID),
                    CONSTRAINT FK_FUA_USER   FOREIGN KEY (USER_ID)   REFERENCES APP_USER(ID),
                    CONSTRAINT FK_FUA_UNIT   FOREIGN KEY (UNIT_ID)   REFERENCES ORGANIZATION_UNIT(ID)
                )", 955);

            // Tạo các indexes
            // ORA-00955: name is already used by an existing object
            ExecuteNonQuery("CREATE INDEX IDX_FUA_FOLDER_ID ON FOLDER_USER_ALLOCATIONS(FOLDER_ID)", 955);
            ExecuteNonQuery("CREATE INDEX IDX_FUA_USER_ID ON FOLDER_USER_ALLOCATIONS(USER_ID)", 955);
            ExecuteNonQuery("CREATE INDEX IDX_FUA_UNIT_ID ON FOLDER_USER_ALLOCATIONS(UNIT_ID)", 955);
            ExecuteNonQuery("CREATE INDEX IDX_FUA_STATUS ON FOLDER_USER_ALLOCATIONS(STATUS)", 955);
            ExecuteNonQuery("CREATE INDEX IDX_FUA_IS_DELETED ON FOLDER_USER_ALLOCATIONS(IS_DELETED)", 955);
        }

        return string.Empty;
    }
}
