using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.DigitizationService;

/// <summary>
/// Thêm cột SOURCE_INDEX cho OCR_MODULE_REGION — lưu vị trí (index) của box trong mảng JSON gốc
/// trên MinIO ("{filePath}_page_{n}.json") mà OcrJsonMaterializer đã đọc để tạo dòng này.
///
/// Vì sao cần: tính năng sửa tay 1 box (tab "Kiểm tra chính tả và hiệu chỉnh nội dung") phải ghi
/// đè lại đúng phần tử trong file JSON gốc trên MinIO rồi dựng lại PDF — không thể match ngược
/// theo toạ độ box (đã quy đổi DPI 150→200 qua phép nhân float, không round-trip chính xác, dễ
/// nhầm khi 2 box gần nhau). Lưu thẳng index là chính xác, rẻ.
///
/// Cột NULL cho các Job đã materialize trước migration này — các Job đó không patch được ngược
/// vào MinIO, tính năng sửa sẽ chặn có kiểm soát (409 ERR_OCR_MODULE_REGION_NOT_PATCHABLE) thay vì
/// suy đoán sai vị trí.
/// </summary>
public class Migration0004_OcrModuleRegionSourceIndex : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        void ExecuteNonQuery(string sql, params int[] ignoreErrorCodes)
        {
            try
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                var ignored = false;
                foreach (var code in ignoreErrorCodes)
                {
                    if (ex.Message.Contains($"ORA-{code:D5}", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains($"ORA-0{code}", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains($"ORA-{code}", StringComparison.OrdinalIgnoreCase))
                    {
                        ignored = true;
                        break;
                    }
                }

                if (!ignored)
                    throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
            }
        }

        // ORA-01430: cột đã tồn tại (chạy lại migration này trên DB đã có cột).
        ExecuteNonQuery("ALTER TABLE OCR_MODULE_REGION ADD SOURCE_INDEX NUMBER", 1430);

        return string.Empty;
    }
}
