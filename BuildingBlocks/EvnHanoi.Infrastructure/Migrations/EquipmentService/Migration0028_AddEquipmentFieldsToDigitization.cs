using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Thêm trường IsEquipmentProfile và EquipmentId để bóc tách theo EAV thiết bị.
/// </summary>
public class Migration0028_AddEquipmentFieldsToDigitization : IScript
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

        // 1. Thêm cột IS_EQUIPMENT_PROFILE vào DOCUMENT_TYPES
        ExecuteNonQuery("ALTER TABLE DOCUMENT_TYPES ADD IS_EQUIPMENT_PROFILE NUMBER(1) DEFAULT 0 NOT NULL", 1430);

        // 2. Thêm cột EQUIPMENT_ID vào DOCUMENT_EXTRACTION_RESULTS
        ExecuteNonQuery("ALTER TABLE DOCUMENT_EXTRACTION_RESULTS ADD EQUIPMENT_ID VARCHAR2(36)", 1430);

        // 3. Thêm khoá ngoại và Index cho EQUIPMENT_ID
        ExecuteNonQuery("ALTER TABLE DOCUMENT_EXTRACTION_RESULTS ADD CONSTRAINT FK_DOC_EXT_RES_EQUIPMENT FOREIGN KEY (EQUIPMENT_ID) REFERENCES Equipments(Id)", 2275);
        ExecuteNonQuery("CREATE INDEX IDX_DOC_EXT_RES_EQ_ID ON DOCUMENT_EXTRACTION_RESULTS(EQUIPMENT_ID)", 955, 1408);

        return string.Empty;
    }
}
