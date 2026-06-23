using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0015_CreateDocumentTypesTable : IScript
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

        ExecuteNonQuery(@"
CREATE TABLE DOCUMENT_TYPES (
    ID VARCHAR2(36) NOT NULL PRIMARY KEY,
    NAME VARCHAR2(255) NOT NULL,
    CODE VARCHAR2(100) NOT NULL UNIQUE,
    FORM_ID VARCHAR2(36) NULL,
    IS_ACTIVE NUMBER(1) DEFAULT 1 NOT NULL,
    PIORITY NUMBER NULL,
    CreatedBy VARCHAR2(100) NULL,
    CreatedDate TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL,
    ModifiedBy VARCHAR2(100) NULL,
    ModifiedDate TIMESTAMP NULL,
    IsDeleted NUMBER(1) DEFAULT 0 NOT NULL,
    CONSTRAINT fk_documenttype_form FOREIGN KEY (FORM_ID) REFERENCES EavFormTemplates(Id) ON DELETE SET NULL
)", 955);

        return string.Empty;
    }
}
