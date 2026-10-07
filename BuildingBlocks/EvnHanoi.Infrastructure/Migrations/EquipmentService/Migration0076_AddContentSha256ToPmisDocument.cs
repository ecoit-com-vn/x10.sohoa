using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Thêm vào PMIS_DOCUMENT: FILE_TRANSIENT_FAILS, DEVICE_CODE và CONTENT_SHA256 (hex 64 ký tự): băm nội dung file khi tải về để (1) chống lưu trùng — nhiều tài liệu
/// PMIS có nội dung giống hệt, (2) kiểm tra toàn vẹn. Dòng cũ để NULL (không tính lại; chỉ file tải sau này mới có hash).
/// Index (CONTENT_SHA256, FileSize) phục vụ tra "đã có file này chưa". Idempotent (ORA-01430/ORA-00955).
/// </summary>
public class Migration0076_AddContentSha256ToPmisDocument : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        // Chờ tối đa 60 giây nếu phiên khác (vd job đang UPDATE bảng) giữ khoá, thay vì thất bại ngay ORA-00054 làm pod crash-loop khi deploy.
        Execute(dbCommandFactory, "ALTER SESSION SET DDL_LOCK_TIMEOUT = 60", "ORA-00000");
        Execute(dbCommandFactory, "ALTER TABLE PMIS_DOCUMENT ADD CONTENT_SHA256 VARCHAR2(64) NULL", "ORA-01430");
        Execute(dbCommandFactory, "CREATE INDEX IDX_PMIS_DOCUMENT_SHA256 ON PMIS_DOCUMENT (CONTENT_SHA256, FileSize)", "ORA-00955");
        // Số lần tải lỗi TẠM THỜI liên tiếp (5xx/429/timeout) — không tính vào FILE_ATTEMPTS nhưng chặn thử lại vô hạn (đủ ngưỡng → coi là lỗi của tài liệu).
        Execute(dbCommandFactory, "ALTER TABLE PMIS_DOCUMENT ADD FILE_TRANSIENT_FAILS NUMBER(5) DEFAULT 0 NOT NULL", "ORA-01430");
        // Mã thiết bị PMIS kèm tài liệu: khi tài liệu được lưu trước lúc thiết bị tồn tại (gán tạm cho Trạm/Đường dây), thiết bị tạo sau sẽ
        // nhận lại tài liệu theo mã này — job danh sách tài liệu không còn đẩy lại mọi tài liệu mỗi chu kỳ nên không tự sửa được nữa.
        Execute(dbCommandFactory, "ALTER TABLE PMIS_DOCUMENT ADD DEVICE_CODE VARCHAR2(150) NULL", "ORA-01430");
        Execute(dbCommandFactory, "CREATE INDEX IDX_PMIS_DOCUMENT_DEVICE_CODE ON PMIS_DOCUMENT (DEVICE_CODE)", "ORA-00955");
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
            // Đã ở đúng trạng thái mong muốn (chạy lại migration thủ công) — bỏ qua.
        }
    }
}
