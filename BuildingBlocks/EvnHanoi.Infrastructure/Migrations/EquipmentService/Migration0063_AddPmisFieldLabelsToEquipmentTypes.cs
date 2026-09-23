using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Bổ sung EquipmentTypes.PmisFieldLabels (CLOB) — bản sao "theo LOẠI thiết bị" của
/// EQUIPMENT_PMIS_SPEC.FieldLabels (Migration0062, lưu theo TỪNG thiết bị). Nhãn tiếng Việt của 1 khoá
/// thongSoKyThuat (vd. "I_DM" → "Dòng điện định mức") là hằng số theo loại thiết bị, không đổi theo
/// từng thiết bị cụ thể — review 2026-09-23 chỉ ra việc chỉ lưu theo từng thiết bị khiến dữ liệu gần
/// như giống hệt nhau bị ghi lặp lại hàng nghìn lần (1 lần/thiết bị) và
/// EquipmentController.GetPmisSpecKeys phải quét tới 50 dòng thiết bị + gộp "khoá nào có nhãn không rỗng
/// đầu tiên thì thắng" mỗi lần gọi, chỉ để tìm ra đúng 1 bộ nhãn lẽ ra cố định theo loại.
///
/// Cột này được ghi 1 LẦN/loại thiết bị (kiểu "set nếu còn rỗng", giống
/// EquipmentRepository.SetFormValuesIfEmptyAsync) ngay khi đồng bộ thiết bị đầu tiên của loại đó có
/// nhãn — GetPmisSpecKeys ưu tiên đọc thẳng cột này (1 dòng, không cần quét/gộp) khi đã có, chỉ quét lại
/// theo từng thiết bị (như trước Migration0062) khi loại thiết bị CHƯA được ghi cột này lần nào.
/// EQUIPMENT_PMIS_SPEC.FieldLabels vẫn giữ nguyên, không xoá — panel "So sánh với PMIS" (GetPmisSpecDiff)
/// vẫn cần nhãn gắn theo đúng lần đồng bộ của riêng 1 thiết bị.
/// </summary>
public class Migration0063_AddPmisFieldLabelsToEquipmentTypes : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        try
        {
            cmd.CommandText = "ALTER TABLE EquipmentTypes ADD (PmisFieldLabels CLOB NULL)";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-01430", StringComparison.OrdinalIgnoreCase))
        {
            // Cột đã tồn tại.
        }

        return string.Empty;
    }
}
