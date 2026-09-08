using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Tạo bảng PMIS_API_CALL_LOG — ghi lại MỌI lần gọi PMIS thật (thành công lẫn thất bại) qua
/// PmisClient.SendAsync/DownloadDocumentFileAsync: API nào, URL/payload thật đã gọi, trạng thái trả
/// về, lỗi nếu có, thời gian gọi — hiển thị qua màn "Cấu hình kết nối API" (nút "Lịch sử gọi").
/// Bảng append-only (không sửa/xoá mềm từng dòng) nên không có cột MODIFIED_*/IS_DELETED, giống
/// SYNC_HISTORY_DETAIL. Dọn tự động sau 30 ngày qua PmisApiCallLogCleanupJob (Quartz).
/// </summary>
public class Migration0008_CreatePmisApiCallLogTable : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory, @"
            CREATE TABLE PMIS_API_CALL_LOG (
                Id              VARCHAR2(36)   NOT NULL PRIMARY KEY,
                ApiCode         VARCHAR2(50)   NOT NULL,
                HttpMethod      VARCHAR2(10)   NOT NULL,
                Url             VARCHAR2(2000) NOT NULL,
                RequestPayload  NVARCHAR2(2000) NULL,
                StatusCode      NUMBER         NULL,
                IsSuccess       NUMBER(1)      NOT NULL,
                ErrorMessage    NVARCHAR2(2000) NULL,
                DurationMs      NUMBER         NOT NULL,
                HttpClientName  VARCHAR2(30)   NULL,
                CalledAt        TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL
            )", "ORA-00955");

        Execute(dbCommandFactory,
            "CREATE INDEX IDX_PMIS_API_CALL_LOG_CODE_TIME ON PMIS_API_CALL_LOG (ApiCode, CalledAt DESC)",
            "ORA-00955");

        return string.Empty;
    }

    private static void Execute(Func<IDbCommand> dbCommandFactory, string sql, string ignoreOraCode)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains(ignoreOraCode, StringComparison.OrdinalIgnoreCase))
        {
            // Bảng/index đã tồn tại (chạy lại migration thủ công) — bỏ qua.
        }
    }
}
