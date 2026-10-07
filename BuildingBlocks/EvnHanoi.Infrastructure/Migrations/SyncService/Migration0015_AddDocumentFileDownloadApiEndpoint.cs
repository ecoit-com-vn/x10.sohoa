using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Bổ sung API thứ 11 'DOCUMENT_FILE_DOWNLOAD' (API tải file tài liệu: GET .../api/PmisDongBo/TaiFileTaiLieu?maTaiLieu=...) vào
/// PMIS_API_ENDPOINT_CONFIG, và hạ PAGE_SIZE 2 API danh sách tài liệu xuống 10.
///
/// BỐI CẢNH (2026-10): PMIS bỏ dùng trường "file" của 2 API danh sách tài liệu (SUBSTATION_DOCUMENT_LIST / LINE_DOCUMENT_LIST) —
/// trước là link, rồi base64 (hàng MB/tài liệu) — và chuyển sang 1 API cố định TaiFileTaiLieu theo mã tài liệu. Job
/// PmisDocumentFileDownloadJob giờ gọi API này theo cấu hình (URL/phương thức/timeout/header chỉnh trên màn "Cấu hình kết nối API")
/// thay vì URL lưu theo từng tài liệu. Phải nới CHECK constraint cũ (chỉ cho phép 10 mã) mới insert được mã mới.
///
/// Dòng seed IS_ACTIVE = 0 và URL rỗng — admin tự nhập URL (vd https://&lt;gateway&gt;/api/PmisDongBo/TaiFileTaiLieu, KHÔNG kèm tham số
/// maTaiLieu — code tự thêm) rồi bật. Chưa bật thì job tải file tự dừng, không tăng số lần thử của tài liệu nào.
///
/// PAGE_SIZE: payload trang danh sách tài liệu từng chứa base64 của file (trang 100 tài liệu = hàng chục–trăm MB, vượt timeout
/// 60 giây, lượt Trạm/Đường dây bị watchdog đánh FAILED). Hạ về 10 — CHỈ khi đang NULL hoặc lớn hơn 10 (không ghi đè giá trị admin
/// đã đặt nhỏ hơn); admin chỉnh lại được trên UI.
///
/// Idempotent: DROP/ADD constraint bắt ORA-02443/ORA-02264, INSERT bắt ORA-00001, UPDATE PAGE_SIZE chỉ chạm dòng còn &gt; 10.
/// </summary>
public class Migration0015_AddDocumentFileDownloadApiEndpoint : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory, "ALTER TABLE PMIS_API_ENDPOINT_CONFIG DROP CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE", "ORA-02443");

        Execute(dbCommandFactory, @"
            ALTER TABLE PMIS_API_ENDPOINT_CONFIG ADD CONSTRAINT CK_PMIS_API_ENDPOINT_CONFIG_CODE CHECK (API_CODE IN (
                'SUBSTATION_LIST', 'LINE_LIST', 'SUBSTATION_DEVICE_TYPE_LIST', 'SUBSTATION_DEVICE_LIST',
                'LINE_DEVICE_TYPE_LIST', 'LINE_DEVICE_LIST', 'DEVICE_DETAIL',
                'SUBSTATION_DOCUMENT_LIST', 'LINE_DOCUMENT_LIST', 'DEVICE_QR_IMAGE', 'DOCUMENT_FILE_DOWNLOAD'
            ))", "ORA-02264");

        using (var command = dbCommandFactory())
        {
            command.CommandText = @"
                INSERT INTO PMIS_API_ENDPOINT_CONFIG (ID, API_CODE, DISPLAY_NAME, HTTP_METHOD, TIMEOUT_SECONDS, IS_ACTIVE)
                VALUES (:Id, 'DOCUMENT_FILE_DOWNLOAD', 'API tải file tài liệu', 'GET', 120, 0)";
            AddParameter(command, "Id", Guid.CreateVersion7().ToString());

            try
            {
                command.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase))
            {
                // Đã tồn tại (chạy lại migration thủ công) — bỏ qua.
            }
        }

        Execute(dbCommandFactory, @"
            UPDATE PMIS_API_ENDPOINT_CONFIG SET PAGE_SIZE = 10
            WHERE API_CODE IN ('SUBSTATION_DOCUMENT_LIST', 'LINE_DOCUMENT_LIST') AND (PAGE_SIZE IS NULL OR PAGE_SIZE > 10)", null);

        return string.Empty;
    }

    private static void Execute(Func<IDbCommand> dbCommandFactory, string sql, string? ignoreOraCode)
    {
        using var command = dbCommandFactory();
        try
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ignoreOraCode != null && ex.Message.Contains(ignoreOraCode, StringComparison.OrdinalIgnoreCase))
        {
            // Đã ở đúng trạng thái mong muốn (chạy lại migration thủ công) — bỏ qua.
        }
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
