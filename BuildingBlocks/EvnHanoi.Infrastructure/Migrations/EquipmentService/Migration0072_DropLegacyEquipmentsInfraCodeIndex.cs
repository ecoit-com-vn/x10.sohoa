using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Xoá nốt UNIQUE INDEX cũ UQ_EQUIPMENTS_INFRA_CODE (INFRASTRUCTURE_ID, CODE) trên EQUIPMENTS.
///
/// Migration0059_FixEquipmentsInfraCodeUnique chỉ chạy "ALTER TABLE ... DROP CONSTRAINT
/// UQ_EQUIPMENTS_INFRA_CODE" — trên production/UAT constraint đó đã mất nhưng UNIQUE INDEX cùng tên (không
/// điều kiện, tính cả dòng đã xoá mềm lẫn dòng "hồn ma" StatusTransition=0) vẫn còn nguyên, độc lập với
/// constraint. Hậu quả đã gặp thật 2026-10-01: mọi lượt đồng bộ Thiết bị đều thất bại lặp lại đúng cùng
/// 242 bản ghi (ORA-00001 "mã ... đã được dùng cho 1 thiết bị khác đang hoạt động") vì dòng hồn ma cũ vẫn
/// chiếm ô (INFRASTRUCTURE_ID, CODE) trong index này, dù về nghiệp vụ không có gì trùng cả — trong khi bước
/// tra cứu (StatusTransition IS NULL) không thấy dòng hồn ma nên cứ INSERT lại.
///
/// Việc chặn trùng thật vẫn do UX_EQUIPMENTS_ACTIVE_INFRA_CODE (Migration0059) đảm nhiệm — index hàm chỉ
/// tính dòng "sống" (IsDeleted=0 AND StatusTransition IS NULL). Vì vậy migration này CHỦ ĐỘNG DỪNG (ném
/// lỗi, không xoá gì) nếu index thay thế đó chưa tồn tại, tránh để bảng mất hoàn toàn khả năng chống trùng.
///
/// Idempotent: chạy lại khi index cũ đã mất thì bỏ qua (ORA-01418). Dùng DDL_LOCK_TIMEOUT để chờ tối đa
/// 60 giây nếu lúc chạy có phiên khác (vd. lượt đồng bộ) đang giữ khoá bảng, thay vì thất bại ngay
/// ORA-00054.
/// </summary>
public class Migration0072_DropLegacyEquipmentsInfraCodeIndex : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var check = dbCommandFactory())
        {
            check.CommandText =
                "SELECT COUNT(*) FROM USER_INDEXES WHERE INDEX_NAME = 'UX_EQUIPMENTS_ACTIVE_INFRA_CODE' AND UNIQUENESS = 'UNIQUE'";
            var replacementExists = Convert.ToInt32(check.ExecuteScalar());
            if (replacementExists == 0)
            {
                throw new InvalidOperationException(
                    "Không thể xoá UQ_EQUIPMENTS_INFRA_CODE: index thay thế UX_EQUIPMENTS_ACTIVE_INFRA_CODE (Migration0059) chưa tồn tại — bảng sẽ không còn chống trùng (INFRASTRUCTURE_ID, CODE). Chạy Migration0059 trước.");
            }
        }

        Execute(dbCommandFactory, "ALTER SESSION SET DDL_LOCK_TIMEOUT = 60", null);

        // Constraint (nếu còn) phải bỏ trước, nếu không DROP INDEX báo ORA-02429.
        Execute(dbCommandFactory, "ALTER TABLE EQUIPMENTS DROP CONSTRAINT UQ_EQUIPMENTS_INFRA_CODE", "ORA-02443");
        Execute(dbCommandFactory, "DROP INDEX UQ_EQUIPMENTS_INFRA_CODE", "ORA-01418");

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
}
