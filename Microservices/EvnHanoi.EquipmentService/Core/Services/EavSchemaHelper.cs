using System.Text.Json;

namespace EvnHanoi.EquipmentService.Core.Services;

/// <summary>
/// Đọc field từ FormSchema JSON (mảng field của EAV form template) — dùng chung giữa
/// EquipmentController.GetPmisSpecDiff và luồng mặc định-hoá thông số kỹ thuật khi đồng bộ PMIS
/// (InternalPmisSyncController), tránh 2 cài đặt trùng lặp có thể lệch nhau.
/// </summary>
public static class EavSchemaHelper
{
    public static IEnumerable<JsonElement> EnumerateSchemaFields(string formSchemaJson)
    {
        using var doc = JsonDocument.Parse(formSchemaJson);
        var root = doc.RootElement;
        var arrayElement = root.ValueKind == JsonValueKind.Array
            ? root
            : (root.TryGetProperty("fields", out var fieldsProp) ? fieldsProp : default);

        if (arrayElement.ValueKind != JsonValueKind.Array) yield break;

        foreach (var field in arrayElement.EnumerateArray())
        {
            // Clone vì JsonDocument gốc sẽ bị dispose khi ra khỏi using — cần giữ lại để dùng bên ngoài.
            yield return field.Clone();
        }
    }

    /// <summary>Khoá field nội bộ dùng trong EQUIPMENTS.FormValues — ưu tiên name → key → id → fieldName.</summary>
    public static string? ResolveSchemaFieldName(JsonElement field) =>
        ReadSchemaString(field, "name", "Name") ??
        ReadSchemaString(field, "key", "Key") ??
        ReadSchemaString(field, "id", "Id") ??
        ReadSchemaString(field, "fieldName", "FieldName");

    public static string? ReadSchemaString(JsonElement field, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (TryGetPropertyIgnoreCase(field, name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var str = value.GetString();
                if (!string.IsNullOrWhiteSpace(str)) return str;
            }
        }
        return null;
    }

    public static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Parse 1 chuỗi JSON object dạng "khoá → nhãn chuỗi" (vd. field PMIS "tenThongSoKyThuat":
    /// {"I_DM":"Dòng điện định mức",...}) và gộp vào <paramref name="target"/> — dùng chung giữa
    /// EquipmentController.GetPmisSpecKeys và InternalPmisSyncController.BuildAutoFormFieldsFromPmisSpec
    /// (trước đây 2 nơi tự viết lại logic này, lệch nhau ở việc có nhận value non-string hay không).
    /// Chỉ nhận value kiểu chuỗi khác rỗng (nhãn luôn là text) — value kiểu khác hoặc rỗng bị bỏ qua.
    /// JSON không hợp lệ hoặc không phải object thì bỏ qua toàn bộ, không ném lỗi ra ngoài. Giữ nguyên
    /// giá trị đã có trong <paramref name="target"/> nếu đã khác rỗng — cho phép gọi lặp lại nhiều dòng
    /// (vd. gộp nhãn từ nhiều thiết bị cùng loại) mà dòng đọc trước luôn thắng.
    /// </summary>
    public static void MergeJsonStringLabelsInto(Dictionary<string, string?> target, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String) continue;
                var label = property.Value.GetString();
                if (string.IsNullOrWhiteSpace(label)) continue;

                if (!target.TryGetValue(property.Name, out var existing) || string.IsNullOrWhiteSpace(existing))
                {
                    target[property.Name] = label;
                }
            }
        }
        catch (JsonException)
        {
            // JSON nhãn không hợp lệ — bỏ qua, không làm hỏng luồng gọi (danh sách gợi ý/biểu mẫu tự tạo).
        }
    }
}
