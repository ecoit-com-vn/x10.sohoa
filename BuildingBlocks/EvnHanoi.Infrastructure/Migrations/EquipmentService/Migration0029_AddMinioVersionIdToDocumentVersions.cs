using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Thêm trường MINIO_VERSION_ID vào bảng DOCUMENT_VERSIONS để liên kết với versionId của MinIO.
/// </summary>
public class Migration0029_AddMinioVersionIdToDocumentVersions : IScript
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

        // 1. Thêm cột MINIO_VERSION_ID vào DOCUMENT_VERSIONS
        ExecuteNonQuery("ALTER TABLE DOCUMENT_VERSIONS ADD MINIO_VERSION_ID VARCHAR2(100)", 1430);

        return string.Empty;
    }
}
