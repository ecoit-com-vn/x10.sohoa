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
}
