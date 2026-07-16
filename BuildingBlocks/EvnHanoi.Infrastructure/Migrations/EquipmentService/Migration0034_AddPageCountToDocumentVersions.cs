using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Thêm PAGE_COUNT vào DOCUMENT_VERSIONS — số trang file lúc upload.
/// PDF = số trang thực; ảnh = 1; loại khác = 0. Dữ liệu cũ = 0.
/// </summary>
public class Migration0034_AddPageCountToDocumentVersions : IScript
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

        ExecuteNonQuery("ALTER TABLE DOCUMENT_VERSIONS ADD PAGE_COUNT NUMBER DEFAULT 0 NOT NULL", 1430, 904);
        ExecuteNonQuery("UPDATE DOCUMENT_VERSIONS SET PAGE_COUNT = 0 WHERE PAGE_COUNT IS NULL", 904);

        return string.Empty;
    }
}
