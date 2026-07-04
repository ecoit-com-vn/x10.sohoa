using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// DOSSIER_KINDS + KIND_ID trên DOSSIERS. Dữ liệu cũ gán KIND_ID=2 (New).
/// </summary>
public class Migration0024_DossierKinds : IScript
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
            CREATE TABLE DOSSIER_KINDS (
                ID   NUMBER PRIMARY KEY,
                CODE VARCHAR2(50) NOT NULL,
                NAME NVARCHAR2(255) NOT NULL
            )", 955);

        ExecuteNonQuery(@"
            INSERT INTO DOSSIER_KINDS (ID, CODE, NAME)
            SELECT 1, 'Digitization', N'Hồ sơ số hóa' FROM DUAL
            WHERE NOT EXISTS (SELECT 1 FROM DOSSIER_KINDS WHERE ID = 1)", 1);

        ExecuteNonQuery(@"
            INSERT INTO DOSSIER_KINDS (ID, CODE, NAME)
            SELECT 2, 'New', N'Hồ sơ mới' FROM DUAL
            WHERE NOT EXISTS (SELECT 1 FROM DOSSIER_KINDS WHERE ID = 2)", 1);

        ExecuteNonQuery(@"
            ALTER TABLE DOSSIERS ADD (KIND_ID NUMBER DEFAULT 2 NOT NULL)", 1430, 904);

        ExecuteNonQuery(@"
            UPDATE DOSSIERS SET KIND_ID = 2 WHERE KIND_ID IS NULL OR KIND_ID = 0", 904);

        ExecuteNonQuery(@"
            ALTER TABLE DOSSIERS ADD CONSTRAINT FK_DOSSIERS_KIND
            FOREIGN KEY (KIND_ID) REFERENCES DOSSIER_KINDS(ID)", 2275, 2261, 2264);

        ExecuteNonQuery("CREATE INDEX IDX_DOSSIERS_KIND_ID ON DOSSIERS(KIND_ID)", 955);

        return string.Empty;
    }
}
