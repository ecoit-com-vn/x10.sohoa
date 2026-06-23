using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// T4 — snapshot tài liệu trên DOSSIER_VERSIONS; UPLOAD_SESSIONS hỗ trợ upload chunked vào hồ sơ.
/// </summary>
public class Migration0013_DossierDocumentsSupport : IScript
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

        ExecuteNonQuery(
            "ALTER TABLE DOSSIER_VERSIONS ADD DocumentsSnapshotJson CLOB NULL",
            1430);

        ExecuteNonQuery(
            "ALTER TABLE UPLOAD_SESSIONS ADD DOSSIER_ID VARCHAR2(36) NULL",
            1430);

        ExecuteNonQuery(
            "ALTER TABLE UPLOAD_SESSIONS MODIFY (FOLDER_ID NULL)",
            1442);
        return string.Empty;
    }
}
