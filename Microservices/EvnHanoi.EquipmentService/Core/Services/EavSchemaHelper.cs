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
    /// <summary>
    /// Đếm số trường trong FormSchema có giá trị khác nhau giữa dữ liệu nội bộ (localFormValuesJson) và
    /// dữ liệu PMIS (pmisFormValuesJson) — dùng cho cột "Đối chiếu PMIS" ở danh sách thiết bị (tab
    /// "Thiết bị" màn chi tiết Trạm/Đường dây). Trường chưa khai báo mapping PMIS (không khớp
    /// pmisFieldName/key nào trong dữ liệu PMIS) bị bỏ qua, không tính là sai khác — cùng quy tắc với
    /// EquipmentController.GetPmisSpecDiff (chỉ khác là ở đây chỉ cần ĐẾM, không cần trả raw data cho FE
    /// tô màu). Trả về null nếu thiết bị chưa từng đồng bộ PMIS (pmisFormValuesJson rỗng) để phân biệt
    /// "chưa đối chiếu" với "đối chiếu khớp 100%" (0 sai khác).
    /// </summary>
    public static int? CountPmisDifferences(string? formSchemaJson, string? localFormValuesJson, string? pmisFormValuesJson)
    {
        if (string.IsNullOrWhiteSpace(pmisFormValuesJson)) return null;
        if (string.IsNullOrWhiteSpace(formSchemaJson)) return 0;

        JsonDocument pmisDoc;
        JsonDocument? localDoc = null;
        try
        {
            pmisDoc = JsonDocument.Parse(pmisFormValuesJson);
            if (!string.IsNullOrWhiteSpace(localFormValuesJson))
                localDoc = JsonDocument.Parse(localFormValuesJson);
        }
        catch (JsonException)
        {
            return null;
        }

        using var pmisDocDisposable = pmisDoc;
        using var localDocDisposable = localDoc;

        var diffCount = 0;
        foreach (var field in EnumerateSchemaFields(formSchemaJson))
        {
            var localKey = ResolveSchemaFieldName(field);
            if (string.IsNullOrWhiteSpace(localKey)) continue;

            var pmisKey = ReadSchemaString(field, "pmisFieldName", "PmisFieldName");
            var resolvedKey = !string.IsNullOrWhiteSpace(pmisKey) ? pmisKey : localKey;

            if (!TryGetPropertyIgnoreCase(pmisDoc.RootElement, resolvedKey, out var pmisValue))
                continue; // Chưa khai báo mapping PMIS — không tính là sai khác dữ liệu.

            JsonElement? localValue = localDoc != null &&
                TryGetPropertyIgnoreCase(localDoc.RootElement, localKey, out var resolvedLocalValue)
                ? resolvedLocalValue
                : null;

            if (FormatValueForDiff(field, localValue) != FormatValueForDiff(field, pmisValue))
                diffCount++;
        }

        return diffCount;
    }

    /// <summary>
    /// Quy đổi giá trị 1 field về chuỗi hiển thị để so sánh — cố gắng bám theo
    /// dossier-form-schema.util.ts#formatFieldDisplayValue (checkbox/select/number+unit), nhưng KHÔNG cần
    /// khớp chính xác định dạng hiển thị cuối (vd. ngày tháng không cần đổi sang vi-VN locale) vì hàm này
    /// chỉ dùng để so sánh 2 bên — chỉ cần áp dụng NHẤT QUÁN cho cả local lẫn PMIS là đủ.
    /// </summary>
    private static string FormatValueForDiff(JsonElement field, JsonElement? valueElement)
    {
        if (valueElement is not { } value || value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            return "—";

        var rawString = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText()
        };

        if (string.IsNullOrEmpty(rawString)) return "—";

        var type = ReadSchemaString(field, "type", "Type");

        if (string.Equals(type, "checkbox", StringComparison.OrdinalIgnoreCase))
        {
            var isTrue = value.ValueKind == JsonValueKind.True || rawString is "true" or "1";
            return isTrue ? "Có" : "Không";
        }

        if (string.Equals(type, "select", StringComparison.OrdinalIgnoreCase) &&
            TryGetPropertyIgnoreCase(field, "options", out var optionsEl) &&
            optionsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var option in optionsEl.EnumerateArray())
            {
                var optionValue = ReadSchemaString(option, "value", "Value");
                if (string.Equals(optionValue, rawString, StringComparison.Ordinal))
                    return ReadSchemaString(option, "label", "Label") ?? rawString;
            }
            return rawString;
        }

        if (string.Equals(type, "number", StringComparison.OrdinalIgnoreCase))
        {
            var unit = ReadSchemaString(field, "unit", "Unit");
            if (!string.IsNullOrWhiteSpace(unit)) return $"{rawString} {unit}";
        }

        return rawString;
    }

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
