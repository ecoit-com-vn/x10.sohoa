using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Nới cột PRIORITY trong PHYSICAL_SHELF, PHYSICAL_FLOOR, PHYSICAL_BOX từ NUMBER(3) lên NUMBER(9).
///
/// NGUYÊN NHÂN LỖI (production 2026-10-15):
/// Trong Migration0030_RefactorPhysicalStorage, cột PRIORITY được khai báo là NUMBER(3) DEFAULT 1 NOT NULL.
/// Giới hạn của NUMBER(3) trong Oracle là tối đa 999 (-999 đến 999).
/// Khi người dùng import file Excel có từ 1000 dòng trở lên (hoặc giá trị cột Ưu tiên >= 1000), Oracle ném lỗi:
/// ORA-01438: value larger than specified precision allowed for this column.
/// Dẫn đến quá trình import bị dừng đột ngột ở đúng dòng 999 (chỉ tạo được 999 bản ghi).
///
/// GIẢI PHÁP:
/// Tăng kích thước cột PRIORITY lên NUMBER(9) (tối đa 999,999,999) tương ứng với kiểu int (Int32) trong C#.
/// </summary>
public class Migration0077_WidenPhysicalStoragePriority : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory, "ALTER SESSION SET DDL_LOCK_TIMEOUT = 60");
        Execute(dbCommandFactory, "ALTER TABLE PHYSICAL_SHELF MODIFY (PRIORITY NUMBER(9))");
        Execute(dbCommandFactory, "ALTER TABLE PHYSICAL_FLOOR MODIFY (PRIORITY NUMBER(9))");
        Execute(dbCommandFactory, "ALTER TABLE PHYSICAL_BOX MODIFY (PRIORITY NUMBER(9))");
        return string.Empty;
    }

    private static void Execute(Func<IDbCommand> dbCommandFactory, string sql)
    {
        using var command = dbCommandFactory();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
