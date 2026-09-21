using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Sửa lỗi tương tự Migration0054/0058 nhưng cho EQUIPMENTS: UQ_EQUIPMENTS_INFRA_CODE (Migration0038) là
/// UNIQUE constraint thường trên (INFRASTRUCTURE_ID, CODE), tính cả dòng đã xoá mềm (IsDeleted=1) VÀ dòng
/// "đã chuyển TBA" (StatusTransition=0 — xem EquipmentRepository.UpsertFromPmisAsync/
/// CloneForInfrastructureTransferAsync). Khi 1 thiết bị bị chuyển sang Trạm/Đường dây khác, dòng CŨ chỉ
/// được đánh StatusTransition=0 (giữ nguyên INFRASTRUCTURE_ID/CODE) — "hồn ma" này vẫn chiếm vĩnh viễn ô
/// (INFRASTRUCTURE_ID, CODE) trong index, dù mọi truy vấn nghiệp vụ đều coi nó vô hình (luôn lọc
/// StatusTransition IS NULL). Nếu thiết bị đó (hoặc PMIS trả trùng mã cho thiết bị khác) sau này cần INSERT
/// đúng vào ô mà hồn ma cũ đang chiếm, Oracle báo ORA-00001 dù về nghiệp vụ không có gì trùng cả — đã gặp
/// thật với nhiều thiết bị PMIS quay lại đúng trạm đã từng chuyển đi trước đó.
///
/// Thay bằng unique index hàm chỉ tính dòng "sống" (IsDeleted=0 AND StatusTransition IS NULL — ĐÚNG điều
/// kiện UpsertFromPmisAsync dùng để tìm "thiết bị hiện tại"): khi không sống, CẢ 2 biểu thức trả về NULL,
/// Oracle bỏ qua khoá toàn NULL trong unique index nên các dòng hồn ma/đã xoá không còn chặn nhau.
/// </summary>
public class Migration0059_FixEquipmentsInfraCodeUnique : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        // An toàn: chặn sớm với thông báo rõ nghĩa nếu hiện đang có ≥2 dòng "sống" thật sự trùng
        // (InfrastructureId, Code) — CREATE UNIQUE INDEX bên dưới sẽ tự thất bại với ORA-01452 nếu không
        // kiểm trước, khó chẩn đoán hơn nhiều so với báo lỗi rõ nghĩa ngay tại đây.
        Execute(dbCommandFactory, @"
            DECLARE
                duplicate_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO duplicate_count FROM (
                    SELECT INFRASTRUCTURE_ID, CODE FROM EQUIPMENTS
                    WHERE ISDELETED = 0 AND STATUSTRANSITION IS NULL
                      AND INFRASTRUCTURE_ID IS NOT NULL AND CODE IS NOT NULL
                    GROUP BY INFRASTRUCTURE_ID, CODE HAVING COUNT(*) > 1
                );
                IF duplicate_count > 0 THEN
                    RAISE_APPLICATION_ERROR(-20001,
                        'Cannot create active EQUIPMENTS infra+code unique index: active duplicates exist.');
                END IF;
            END;", ignoreOraCode: null);

        Execute(dbCommandFactory,
            "ALTER TABLE EQUIPMENTS DROP CONSTRAINT UQ_EQUIPMENTS_INFRA_CODE",
            "ORA-02443");

        Execute(dbCommandFactory, @"
            CREATE UNIQUE INDEX UX_EQUIPMENTS_ACTIVE_INFRA_CODE ON EQUIPMENTS (
                CASE WHEN ISDELETED = 0 AND STATUSTRANSITION IS NULL THEN INFRASTRUCTURE_ID END,
                CASE WHEN ISDELETED = 0 AND STATUSTRANSITION IS NULL THEN CODE END
            )", "ORA-00955");

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
