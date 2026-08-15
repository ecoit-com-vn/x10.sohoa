using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.DigitizationService;

/// <summary>
/// Thêm cột CURRENT_PAGE cho OCR_MODULE_JOB — phục vụ màn hình "Quản lý dữ liệu huấn luyện AI-OCR"
/// hiển thị % tiến trình OCR (trang đã xử lý / tổng số trang) trong lúc Job ở trạng thái Materializing,
/// theo đúng kiểu hiển thị % đã có ở tab "Tài liệu đính kèm" của hồ sơ.
/// </summary>
public class Migration0005_AddOcrModuleJobCurrentPage : IScript
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

        // ORA-01430: cột đã tồn tại (chạy lại migration này trên DB đã có cột).
        ExecuteNonQuery("ALTER TABLE OCR_MODULE_JOB ADD CURRENT_PAGE NUMBER", 1430);

        return string.Empty;
    }
}
