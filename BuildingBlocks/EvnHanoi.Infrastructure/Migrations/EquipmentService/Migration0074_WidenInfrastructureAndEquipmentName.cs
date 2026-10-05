using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Nới INFRASTRUCTURE.NAME và EQUIPMENTS.NAME từ VARCHAR2(255 BYTE) lên VARCHAR2(512 CHAR), và
/// INFRASTRUCTURE.NORMALIZED_NAME lên VARCHAR2(1000 CHAR).
///
/// LỖI THẬT 2026-10-05 (production): đường dây PD-0022D00-DZ1926622 có tên PMIS ~190 ký tự nhưng 265 BYTE
/// (chữ tiếng Việt có dấu chiếm 2–3 byte UTF-8) → ORA-12899 "value too large for column ... NAME (actual: 265,
/// maximum: 255)". Cột khai báo theo BYTE (CHAR_USED='B') nên giới hạn thật chỉ ~130–255 ký tự tuỳ dấu. Bản ghi
/// này thất bại lặp lại ở MỌI lượt đồng bộ Đường dây (không vào DB được) và làm modal lịch sử luôn hiện "Lỗi
/// của cả lượt đồng bộ". EQUIPMENTS.NAME cùng khai báo 255 BYTE nên thiết bị tên dài sẽ gặp lỗi y hệt.
///
/// Chuyển sang đếm theo KÝ TỰ (CHAR) với 512 — không còn phụ thuộc số byte của dấu tiếng Việt. NORMALIZED_NAME
/// (tên đã bỏ dấu/chuẩn hoá, dùng cho tìm kiếm) nới lên 1000 để luôn chứa được NAME mới (500 BYTE cũ không đủ).
///
/// An toàn: nới cột VARCHAR2 chỉ đổi metadata (không viết lại dữ liệu), giữ nguyên NOT NULL/DEFAULT; đã kiểm
/// tra không có index/view/materialized view nào phụ thuộc các cột này. Idempotent: MODIFY lên đúng kích thước
/// hiện có không báo lỗi. Cần khoá DDL ngắn trên bảng — DDL_LOCK_TIMEOUT chờ tối đa 60 giây nếu có phiên đang
/// ghi, thay vì ORA-00054 ngay.
/// </summary>
public class Migration0074_WidenInfrastructureAndEquipmentName : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory, "ALTER SESSION SET DDL_LOCK_TIMEOUT = 60");
        Execute(dbCommandFactory, "ALTER TABLE INFRASTRUCTURE MODIFY (NAME VARCHAR2(512 CHAR))");
        Execute(dbCommandFactory, "ALTER TABLE INFRASTRUCTURE MODIFY (NORMALIZED_NAME VARCHAR2(1000 CHAR))");
        Execute(dbCommandFactory, "ALTER TABLE EQUIPMENTS MODIFY (NAME VARCHAR2(512 CHAR))");
        return string.Empty;
    }

    private static void Execute(Func<IDbCommand> dbCommandFactory, string sql)
    {
        using var command = dbCommandFactory();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
