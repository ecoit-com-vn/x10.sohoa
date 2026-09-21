using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Chặn trùng PMIS_CODE ở tầng DB — trước đây INFRASTRUCTURE.PMIS_CODE/EQUIPMENTS.PMIS_CODE chỉ có index
/// thường (Migration0047/0048), không UNIQUE. Kết hợp với việc PMIS_CODE không được chuẩn hoá khi ghi
/// (khoảng trắng/hoa-thường tuỳ lần PMIS trả về — đã fix ở PmisSyncExecutionService/UpsertFromPmisAsync,
/// cùng đợt với migration này), 1 lượt sync có mã trạm lệch nhẹ so với giá trị đã lưu có thể khiến hệ
/// thống KHÔNG tìm thấy dòng cũ và tự tạo thêm 1 dòng INFRASTRUCTURE trùng cho CÙNG 1 trạm thật — kéo theo
/// TOÀN BỘ thiết bị của trạm đó bị hiểu nhầm là "chuyển sang trạm mới" (StatusTransition=0, "Đã chuyển
/// TBA") ngay trong lượt kế tiếp — đã gặp thật trên production (1 trạm hiện toàn bộ thiết bị "Đã chuyển
/// TBA" dù không ai thực sự chuyển thiết bị đi).
///
/// Unique index dạng hàm (theo đúng khuôn Migration0054/0058/0059) — chỉ tính dòng "sống", chuẩn hoá
/// UPPER(TRIM(...)) để khớp đúng cách so sánh mới dùng ở UpsertFromPmisAsync:
/// - INFRASTRUCTURE: sống = ISDELETED=0 (không có khái niệm StatusTransition ở bảng này).
/// - EQUIPMENTS: sống = ISDELETED=0 AND STATUSTRANSITION IS NULL (khớp đúng điều kiện "existing" lookup).
///
/// LƯU Ý VẬN HÀNH: nếu môi trường đích ĐANG có ≥2 dòng "sống" trùng PMIS_CODE (đã chuẩn hoá) từ trước khi
/// chạy migration này — đúng tình huống lỗi đang sửa — bước kiểm tra dưới đây sẽ RAISE_APPLICATION_ERROR
/// và toàn bộ migration (cả 2 index) THẤT BẠI có chủ đích, KHÔNG tự động gộp/xoá dữ liệu. Cần người quản
/// trị xác định dòng "chính", chuyển thiết bị của dòng "phụ" về dòng chính, xoá mềm dòng phụ, rồi chạy lại.
/// </summary>
public class Migration0060_AddActivePmisCodeUniqueIndexes : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        CheckNoActiveDuplicates(dbCommandFactory,
            table: "INFRASTRUCTURE",
            whereLive: "ISDELETED = 0",
            entityLabel: "INFRASTRUCTURE (Trạm/Đường dây)");

        Execute(dbCommandFactory, @"
            CREATE UNIQUE INDEX UX_INFRASTRUCTURE_ACTIVE_PMIS_CODE ON INFRASTRUCTURE (
                CASE WHEN ISDELETED = 0 AND PMIS_CODE IS NOT NULL THEN UPPER(TRIM(PMIS_CODE)) END
            )", "ORA-00955");

        CheckNoActiveDuplicates(dbCommandFactory,
            table: "EQUIPMENTS",
            whereLive: "ISDELETED = 0 AND STATUSTRANSITION IS NULL",
            entityLabel: "EQUIPMENTS (Thiết bị)");

        Execute(dbCommandFactory, @"
            CREATE UNIQUE INDEX UX_EQUIPMENTS_ACTIVE_PMIS_CODE ON EQUIPMENTS (
                CASE WHEN ISDELETED = 0 AND STATUSTRANSITION IS NULL AND PMIS_CODE IS NOT NULL THEN UPPER(TRIM(PMIS_CODE)) END
            )", "ORA-00955");

        return string.Empty;
    }

    private static void CheckNoActiveDuplicates(Func<IDbCommand> dbCommandFactory, string table, string whereLive, string entityLabel)
    {
        using var command = dbCommandFactory();
        // AND TRIM(PMIS_CODE) IS NOT NULL: Oracle TRIM('   ') = NULL — nếu không loại, các dòng PMIS_CODE
        // toàn khoảng trắng (PMIS_CODE IS NOT NULL nhưng rỗng sau trim) bị GROUP BY gộp nhầm thành "trùng"
        // giả, dù unique index CASE WHEN...END thật sự KHÔNG BAO GIỜ xung đột trên khoá toàn NULL (Oracle
        // tự bỏ qua) — tránh chặn oan việc tạo index.
        command.CommandText = $@"
            DECLARE
                duplicate_count NUMBER;
            BEGIN
                SELECT COUNT(*) INTO duplicate_count FROM (
                    SELECT UPPER(TRIM(PMIS_CODE)) FROM {table}
                    WHERE {whereLive} AND PMIS_CODE IS NOT NULL AND TRIM(PMIS_CODE) IS NOT NULL
                    GROUP BY UPPER(TRIM(PMIS_CODE)) HAVING COUNT(*) > 1
                );
                IF duplicate_count > 0 THEN
                    RAISE_APPLICATION_ERROR(-20001,
                        'Cannot create active PMIS_CODE unique index on {entityLabel}: active duplicates exist — merge/soft-delete the duplicate rows first.');
                END IF;
            END;";
        command.ExecuteNonQuery();
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
