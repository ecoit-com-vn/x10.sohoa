using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Bổ sung 2 cột còn thiếu trên bảng SYNC_CONFIG (đã có sẵn từ Migration0001) để phục vụ module
/// "thiết lập lịch đồng bộ": ROW_VERSION cho khoá lạc quan khi nhiều người cùng sửa lịch
/// (BACKEND_GUIDELINES §2.5), IS_DELETED cho soft-delete theo chuẩn audit chung (§2.4) — dù 3 dòng
/// SYNC_CONFIG (SUBSTATION/TRANSMISSION_LINE/EQUIPMENT) trên thực tế không bị xoá, chỉ bật/tắt qua
/// IS_ENABLED, cột này vẫn thêm để đồng nhất quy ước audit toàn hệ thống.
/// Rà soát các cột còn lại của SYNC_CONFIG/SYNC_HISTORY/SYNC_HISTORY_DETAIL (Migration0001): không
/// có cột nào dư thừa cần bỏ — kể cả SYNC_HISTORY.OBJECT_TYPE (trùng lặp nhẹ với join qua
/// SYNC_CONFIG_ID) được giữ lại có chủ đích để lọc lịch sử theo đối tượng không cần join.
/// </summary>
public class Migration0003_AddRowVersionAndIsDeletedToSyncConfig : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var command = dbCommandFactory())
        {
            Execute(command, "ALTER TABLE SYNC_CONFIG ADD (ROW_VERSION NUMBER DEFAULT 1 NOT NULL)");
        }

        using (var command = dbCommandFactory())
        {
            Execute(command, "ALTER TABLE SYNC_CONFIG ADD (IS_DELETED NUMBER(1) DEFAULT 0 NOT NULL)");
        }

        using (var command = dbCommandFactory())
        {
            Execute(command, "ALTER TABLE SYNC_CONFIG ADD CONSTRAINT CK_SYNC_CONFIG_DELETED CHECK (IS_DELETED IN (0, 1))");
        }

        return string.Empty;
    }

    private static void Execute(IDbCommand command, string sql)
    {
        try
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (
            ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase) ||   // column already exists
            ex.Message.Contains("ORA-02264", StringComparison.OrdinalIgnoreCase) ||   // constraint name already used
            ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))     // name already used by existing object
        {
            // Cột/ràng buộc đã tồn tại (chạy lại migration hoặc đã áp dụng bằng script .sql dự phòng) — bỏ qua.
        }
    }
}
