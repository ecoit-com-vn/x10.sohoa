using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Bảng trạng thái đồng bộ PMIS tăng dần — mỗi dòng ghi lại hash nội dung lần đẩy thành công gần nhất của
/// 1 bản ghi PMIS để các lượt sau bỏ qua bản ghi không đổi (xem KE_HOACH_DONG_BO_TANG_DAN_PMIS.md).
/// OBJECT_TYPE: SUBSTATION | TRANSMISSION_LINE | EQUIPMENT (hash bản ghi), PARENT_SCAN (chỉ dùng
/// LAST_SCAN_AT: lần quét thiết bị con gần nhất của 1 Trạm/Đường dây cha), SWEEP (PMIS_CODE = loại đối
/// tượng, chỉ dùng LAST_PUSHED_AT: lần quét đầy đủ gần nhất).
/// </summary>
public class Migration0014_CreatePmisSyncStateTable : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        try
        {
            using var cmd = dbCommandFactory();
            cmd.CommandText = @"
                CREATE TABLE PMIS_SYNC_STATE (
                    OBJECT_TYPE     VARCHAR2(30)  NOT NULL,
                    PMIS_CODE       VARCHAR2(150) NOT NULL,
                    CONTENT_HASH    VARCHAR2(64)  NULL,
                    HASH_VERSION    NUMBER(3)     DEFAULT 1 NOT NULL,
                    DETAIL_SYNCED   NUMBER(1)     DEFAULT 0 NOT NULL,
                    LAST_PUSHED_AT  TIMESTAMP     NULL,
                    LAST_SEEN_AT    TIMESTAMP     NULL,
                    LAST_SCAN_AT    TIMESTAMP     NULL,
                    CONSTRAINT PK_PMIS_SYNC_STATE PRIMARY KEY (OBJECT_TYPE, PMIS_CODE),
                    CONSTRAINT CK_PMIS_SYNC_STATE_DETAIL CHECK (DETAIL_SYNCED IN (0, 1))
                )";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Bảng đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }

        return string.Empty;
    }
}
