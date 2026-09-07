using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Thêm CONSECUTIVE_FAILURE_COUNT vào SYNC_CONFIG — đếm số lần đồng bộ tự động lỗi liên tiếp ngay từ
/// bước gọi danh sách (PMIS timeout/401/404/circuit breaker), dùng để PmisScheduledSyncJob backoff tăng
/// dần thay vì retry mỗi phút vô hạn, và để quyết định khi nào cần cảnh báo admin.
/// </summary>
public class Migration0007_AddConsecutiveFailureCountToSyncConfig : IScript
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

        ExecuteNonQuery("ALTER TABLE SYNC_CONFIG ADD CONSECUTIVE_FAILURE_COUNT NUMBER DEFAULT 0 NOT NULL", 1430);

        return string.Empty;
    }
}
