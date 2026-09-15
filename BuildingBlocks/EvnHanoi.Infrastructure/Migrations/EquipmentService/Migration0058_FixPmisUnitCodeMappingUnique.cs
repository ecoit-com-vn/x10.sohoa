using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Sửa lỗi tương tự Migration0054 (PMIS_EQUIPMENT_TYPE_MAPPING) nhưng cho PMIS_UNIT_CODE_MAPPING:
/// UQ_PMIS_UNIT_CODE_MAPPING_CODE (Migration0051) là UNIQUE constraint thường, tính cả dòng đã xoá mềm —
/// giờ đã bổ sung màn quản lý ánh xạ đơn vị có nút xoá (PmisUnitCodeMappingController.Delete), nếu không
/// sửa lại thì sau khi xoá 1 ánh xạ, không thêm lại được đúng mã đơn vị PMIS đó lần nữa (báo trùng dù
/// danh sách hiển thị trống). Thay bằng unique index hàm chỉ tính dòng IsDeleted = 0: khi IsDeleted = 1
/// biểu thức trả về NULL, Oracle bỏ qua khoá toàn NULL trong unique index nên các dòng đã xoá không còn
/// chặn nhau.
/// </summary>
public class Migration0058_FixPmisUnitCodeMappingUnique : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory,
            "ALTER TABLE PMIS_UNIT_CODE_MAPPING DROP CONSTRAINT UQ_PMIS_UNIT_CODE_MAPPING_CODE",
            "ORA-02443");

        Execute(dbCommandFactory, @"
            CREATE UNIQUE INDEX UX_PMIS_UNIT_CODE_MAPPING_ACTIVE ON PMIS_UNIT_CODE_MAPPING (
                CASE WHEN IsDeleted = 0 THEN PmisUnitCode END
            )", "ORA-00955");

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
