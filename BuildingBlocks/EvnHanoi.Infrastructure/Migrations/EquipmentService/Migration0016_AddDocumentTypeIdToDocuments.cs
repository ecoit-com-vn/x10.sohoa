using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Gắn loại văn bản (DOCUMENT_TYPES) cho tài liệu trong hồ sơ.
/// </summary>
public class Migration0016_AddDocumentTypeIdToDocuments : IScript
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
ALTER TABLE DOCUMENTS ADD (
    DOCUMENT_TYPE_ID VARCHAR2(36) NULL
)", 1430);

        ExecuteNonQuery(@"
ALTER TABLE DOCUMENTS ADD CONSTRAINT FK_DOCUMENTS_DOCUMENT_TYPE
    FOREIGN KEY (DOCUMENT_TYPE_ID) REFERENCES DOCUMENT_TYPES(ID)", 2275, 2261, 2264);

        ExecuteNonQuery(@"
CREATE INDEX IDX_DOCUMENTS_DOCUMENT_TYPE_ID ON DOCUMENTS(DOCUMENT_TYPE_ID)", 955, 1408);

        return string.Empty;
    }
}
