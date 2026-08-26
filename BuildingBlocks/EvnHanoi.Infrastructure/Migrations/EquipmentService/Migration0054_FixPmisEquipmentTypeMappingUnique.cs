using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Sửa lỗi: UQ_PMIS_EQUIPMENT_TYPE_MAPPING (Migration0052) tính cả dòng đã xoá mềm nên sau khi admin xoá
/// 1 ánh xạ thì KHÔNG thêm lại được đúng cặp (mã loại thiết bị PMIS + cấp điện áp) đó — danh sách trống
/// mà vẫn báo "đã được ánh xạ". Thay bằng unique index hàm chỉ tính dòng IsDeleted = 0: khi IsDeleted = 1
/// cả 2 biểu thức đều NULL, Oracle bỏ qua khoá toàn NULL trong unique index nên các dòng đã xoá không
/// còn chặn nhau.
///
/// Ghi chú: PMIS_UNIT_CODE_MAPPING (Migration0051) có cùng dạng ràng buộc nhưng chưa có API xoá nên chưa
/// gặp lỗi này — nếu sau này bổ sung màn quản lý ánh xạ đơn vị thì xử lý y hệt.
/// </summary>
public class Migration0054_FixPmisEquipmentTypeMappingUnique : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Execute(dbCommandFactory,
            "ALTER TABLE PMIS_EQUIPMENT_TYPE_MAPPING DROP CONSTRAINT UQ_PMIS_EQUIPMENT_TYPE_MAPPING",
            "ORA-02443");

        Execute(dbCommandFactory, @"
            CREATE UNIQUE INDEX UX_PMIS_EQTYPE_MAPPING_ACTIVE ON PMIS_EQUIPMENT_TYPE_MAPPING (
                CASE WHEN IsDeleted = 0 THEN PmisMaLoaiTB END,
                CASE WHEN IsDeleted = 0 THEN GridTypeId END
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
