using System.Text.Json;

namespace EvnHanoi.EquipmentService.Core.Services;

/// <summary>
/// Gộp mảng kết quả bóc tách theo trang thành một object flat (MERGED_DATA_JSON).
/// Quy tắc: ưu tiên giá trị không null/empty; trang sau ghi đè nếu có giá trị mới.
/// </summary>
public static class ExtractionResultMerger
{
    public static string? MergePageResults(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var pageItem in doc.RootElement.EnumerateArray())
            {
                if (!TryGetDataObject(pageItem, out var dataEl))
                    continue;

                foreach (var prop in dataEl.EnumerateObject())
                {
                    if (!HasMeaningfulValue(prop.Value))
                        continue;

                    merged[prop.Name] = JsonElementToNetValue(prop.Value);
                }
            }

            return merged.Count == 0
                ? null
                : JsonSerializer.Serialize(merged);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetDataObject(JsonElement pageItem, out JsonElement dataEl)
    {
        dataEl = default;
        if (pageItem.ValueKind != JsonValueKind.Object)
            return false;

        if (pageItem.TryGetProperty("data", out dataEl) && dataEl.ValueKind == JsonValueKind.Object)
            return true;

        // Fallback: phần tử là object phẳng (không bọc data)
        if (!pageItem.TryGetProperty("page", out _))
        {
            dataEl = pageItem;
            return dataEl.ValueKind == JsonValueKind.Object;
        }

        return false;
    }

    private static bool HasMeaningfulValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
            _ => true
        };
    }

    private static object? JsonElementToNetValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => JsonSerializer.Deserialize<object>(element.GetRawText())
        };
    }

    /// <summary>
    /// Gộp kết quả bóc tách mới với FormDataJson hồ sơ — giá trị người dùng đã lưu được ưu tiên.
    /// </summary>
    public static string? MergePreservingExisting(string? freshMergedJson, string? existingFormDataJson)
    {
        if (string.IsNullOrWhiteSpace(freshMergedJson))
            return existingFormDataJson;
        if (string.IsNullOrWhiteSpace(existingFormDataJson))
            return freshMergedJson;

        try
        {
            using var freshDoc = JsonDocument.Parse(freshMergedJson);
            using var existingDoc = JsonDocument.Parse(existingFormDataJson);

            if (freshDoc.RootElement.ValueKind != JsonValueKind.Object)
                return existingFormDataJson;

            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in freshDoc.RootElement.EnumerateObject())
            {
                if (!HasMeaningfulValue(prop.Value))
                    continue;
                result[prop.Name] = JsonElementToNetValue(prop.Value);
            }

            if (existingDoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in existingDoc.RootElement.EnumerateObject())
                {
                    if (!HasMeaningfulValue(prop.Value))
                        continue;
                    result[prop.Name] = JsonElementToNetValue(prop.Value);
                }
            }

            return result.Count == 0 ? null : JsonSerializer.Serialize(result);
        }
        catch (JsonException)
        {
            return freshMergedJson;
        }
    }
}
