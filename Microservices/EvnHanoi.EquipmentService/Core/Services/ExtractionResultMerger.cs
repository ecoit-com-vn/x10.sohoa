using System.Text.Json;

namespace EvnHanoi.EquipmentService.Core.Services;

/// <summary>
/// Đọc block <c>merged</c> từ RESULT_JSON format ExtractionWorker: <c>{ merged, pages }</c>.
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
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !TryGetPropertyIgnoreCase(root, "merged", out var mergedEl)
                || mergedEl.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in mergedEl.EnumerateObject())
            {
                if (!HasMeaningfulValue(prop.Value))
                    continue;

                merged[prop.Name] = JsonElementToNetValue(prop.Value);
            }

            return merged.Count == 0 ? null : JsonSerializer.Serialize(merged);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
            return true;

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
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
}
