using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Tách việc đồng bộ DANH SÁCH tài liệu PMIS khỏi việc TẢI FILE VẬT LÝ: PMIS_DOCUMENT giờ đóng vai trò
/// hàng đợi tải file. Pha danh sách chỉ lưu metadata + URL file (FILE_URL, FILE_SOURCE_API = mã endpoint
/// nguồn để lấy đúng header cấu hình), job nền riêng của SyncService (PmisDocumentFileDownloadJob) quét các
/// dòng FILE_STATUS = 'PENDING' rồi tải dần, có thử lại theo backoff (FILE_ATTEMPTS/FILE_NEXT_RETRY_AT).
///
/// FILE_STATUS: NO_URL (PMIS không trả URL / dòng cũ chưa biết URL), PENDING (chờ tải), DONE (đã có file),
/// FAILED (quá số lần thử tối đa, dừng tự thử lại). Dòng đã có ObjectKey được backfill thành DONE.
/// </summary>
public class Migration0069_AddFileDownloadStateToPmisDocument : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        var alters = new[]
        {
            "ALTER TABLE PMIS_DOCUMENT ADD FILE_URL VARCHAR2(1000) NULL",
            "ALTER TABLE PMIS_DOCUMENT ADD FILE_SOURCE_API VARCHAR2(50) NULL",
            "ALTER TABLE PMIS_DOCUMENT ADD FILE_STATUS VARCHAR2(20) DEFAULT 'NO_URL' NOT NULL",
            "ALTER TABLE PMIS_DOCUMENT ADD FILE_ATTEMPTS NUMBER DEFAULT 0 NOT NULL",
            "ALTER TABLE PMIS_DOCUMENT ADD FILE_LAST_ERROR NVARCHAR2(2000) NULL",
            "ALTER TABLE PMIS_DOCUMENT ADD FILE_NEXT_RETRY_AT TIMESTAMP NULL",
        };

        foreach (var sql in alters)
        {
            try
            {
                using var cmd = dbCommandFactory();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
            {
                // Cột đã tồn tại (chạy lại migration thủ công) — bỏ qua.
            }
        }

        using (var backfill = dbCommandFactory())
        {
            backfill.CommandText = "UPDATE PMIS_DOCUMENT SET FILE_STATUS = 'DONE' WHERE ObjectKey IS NOT NULL AND FILE_STATUS = 'NO_URL'";
            backfill.ExecuteNonQuery();
        }

        try
        {
            using var idx = dbCommandFactory();
            idx.CommandText = "CREATE INDEX IDX_PMIS_DOCUMENT_FILE_QUEUE ON PMIS_DOCUMENT (FILE_STATUS, FILE_NEXT_RETRY_AT)";
            idx.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Index đã tồn tại.
        }

        return string.Empty;
    }
}
