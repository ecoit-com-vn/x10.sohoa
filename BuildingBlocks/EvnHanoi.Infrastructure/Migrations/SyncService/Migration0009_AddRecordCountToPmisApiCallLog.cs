using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Thêm RECORD_COUNT vào PMIS_API_CALL_LOG — số bản ghi (Items.Count) trả về trong response của các
/// API dạng danh sách khi gọi thành công, hiển thị kèm trạng thái ở màn "Lịch sử gọi API" thay vì chỉ có
/// mã HTTP. Null với các API không phải danh sách (ChiTietThietBi, AnhQRCode) hoặc khi gọi lỗi.
/// </summary>
public class Migration0009_AddRecordCountToPmisApiCallLog : IScript
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

        ExecuteNonQuery("ALTER TABLE PMIS_API_CALL_LOG ADD RECORD_COUNT NUMBER NULL", 1430);

        return string.Empty;
    }
}
