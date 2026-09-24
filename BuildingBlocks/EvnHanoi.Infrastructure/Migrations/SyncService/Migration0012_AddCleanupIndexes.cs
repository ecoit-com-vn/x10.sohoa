using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Thêm index cho 2 cột mốc thời gian dùng trong job dọn dẹp định kỳ (audit PMIS 2026-09-24) —
/// PmisApiCallLogRepository.DeleteOlderThanAsync và SyncHistoryRepository.DeleteOlderThanAsync đều DELETE
/// theo <c>WHERE &lt;cột thời gian&gt; &lt; SYSTIMESTAMP - :RetentionDays</c>, nhưng không có index nào có
/// cột thời gian này làm leading column — mỗi lần job chạy phải full table scan, ngày càng chậm khi bảng
/// tích luỹ dữ liệu theo thời gian (đúng lúc job dọn dẹp cần chạy nhất):
/// - IX_PMIS_API_CALL_LOG_CALLED_AT (CalledAt) trên PMIS_API_CALL_LOG.
/// - IX_SYNC_HISTORY_START_TIME (START_TIME) trên SYNC_HISTORY.
///
/// Đặt ở migration RIÊNG trong Migrations/SyncService (không gộp vào migration EquipmentService cùng đợt
/// audit) vì PMIS_API_CALL_LOG/SYNC_HISTORY do SyncService sở hữu/migrate (Migration0001/0008) — tuy chung
/// 1 schema/DB Oracle QLSHX10 với EquipmentService, SyncService chạy migration qua kết nối/DbUp runner
/// riêng của chính nó.
///
/// Chỉ CỘNG THÊM (CREATE INDEX), không đụng dữ liệu/constraint hiện có. Idempotent qua bắt ORA-00955 (index
/// đã tồn tại), cùng khuôn EquipmentService/Migration0064_AddNormalizedPmisCodeIndexes.
/// </summary>
public class Migration0012_AddCleanupIndexes : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory,
            "CREATE INDEX IX_PMIS_API_CALL_LOG_CALLED_AT ON PMIS_API_CALL_LOG (CalledAt)");
        Execute(dbCommandFactory,
            "CREATE INDEX IX_SYNC_HISTORY_START_TIME ON SYNC_HISTORY (START_TIME)");

        return string.Empty;
    }

    private static void Execute(Func<IDbCommand> dbCommandFactory, string sql)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Index đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }
    }
}
