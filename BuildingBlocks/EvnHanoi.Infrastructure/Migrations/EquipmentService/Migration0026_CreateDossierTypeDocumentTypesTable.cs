using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0026_CreateDossierTypeDocumentTypesTable : IScript
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
                    if (ex.Message.Contains($"ORA-{code:D5}") || ex.Message.Contains($"ORA-0{code}") || ex.Message.Contains($"ORA-{code}"))
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
            CREATE TABLE DOSSIER_TYPE_DOCUMENT_TYPES (
                DOSSIER_TYPE_ID VARCHAR2(36) NOT NULL,
                DOCUMENT_TYPE_ID VARCHAR2(36) NOT NULL,
                PRIMARY KEY (DOSSIER_TYPE_ID, DOCUMENT_TYPE_ID),
                CONSTRAINT fk_dtdt_dossier_type FOREIGN KEY (DOSSIER_TYPE_ID) REFERENCES DOSSIER_TYPES(ID) ON DELETE CASCADE,
                CONSTRAINT fk_dtdt_document_type FOREIGN KEY (DOCUMENT_TYPE_ID) REFERENCES DOCUMENT_TYPES(ID) ON DELETE CASCADE
            )", 955);

        return string.Empty;
    }
}
