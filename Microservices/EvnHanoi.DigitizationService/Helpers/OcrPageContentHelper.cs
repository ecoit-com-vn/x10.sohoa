using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using EvnHanoi.DigitizationService.Workers;

namespace EvnHanoi.DigitizationService.Helpers;

internal static class OcrPageContentHelper
{
    internal static readonly JsonSerializerOptions OcrJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal static readonly JsonSerializerOptions Utf8JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    internal static List<TextBoxResponse> DeserializeOcrResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<TextBoxResponse>>(responseBody, OcrJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static TextBoxResponse CreateFullPageBox(string text) => new()
    {
        Text = NormalizeUtf8Text(text),
        Box = [0, 0, 1000, 1000],
        Confidence = 1.0f,
    };

    internal static string NormalizeUtf8Text(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormC);

        // Sửa trường hợp UTF-8 bị đọc nhầm thành Latin-1 (mojibake: Ã, Â, ï¿½...)
        if (LooksLikeMojibake(normalized))
        {
            try
            {
                var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(normalized);
                var repaired = Encoding.UTF8.GetString(bytes);
                if (!string.IsNullOrWhiteSpace(repaired) && !repaired.Contains('\uFFFD'))
                    normalized = repaired.Normalize(NormalizationForm.FormC);
            }
            catch (DecoderFallbackException)
            {
                // Giữ nguyên bản gốc nếu không sửa được.
            }
        }

        return normalized.Trim();
    }

    internal static bool IsEmptyOcrJson(string? jsonText)
    {
        if (string.IsNullOrWhiteSpace(jsonText))
            return true;

        try
        {
            var arr = JsonNode.Parse(jsonText)?.AsArray();
            return arr == null || arr.Count == 0;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    internal static string ToCompactPageJson(IReadOnlyList<TextBoxResponse> boxes)
    {
        var compactBoxes = new JsonArray();
        foreach (var box in boxes)
        {
            if (box.Box == null || box.Box.Count != 4)
                continue;

            var text = NormalizeUtf8Text(box.Text);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            compactBoxes.Add(new JsonObject
            {
                ["Text"] = text,
                ["Box"] = new JsonArray(
                    Math.Round(box.Box[0]),
                    Math.Round(box.Box[1]),
                    Math.Round(box.Box[2]),
                    Math.Round(box.Box[3])),
            });
        }

        return compactBoxes.ToJsonString(Utf8JsonOptions);
    }

    private static bool LooksLikeMojibake(string text) =>
        text.Contains('Ã') || text.Contains('Â') || text.Contains('\uFFFD');

    internal static string StripMarkdownCodeFence(string? jsonText)
    {
        if (string.IsNullOrWhiteSpace(jsonText)) return string.Empty;

        var extracted = jsonText.Trim();
        if (extracted.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            extracted = extracted.Substring(7);
        }
        else if (extracted.StartsWith("```"))
        {
            int nl = extracted.IndexOf('\n');
            extracted = nl >= 0 ? extracted.Substring(nl + 1) : extracted;
        }

        extracted = extracted.Trim();
        if (extracted.EndsWith("```"))
        {
            extracted = extracted.Substring(0, extracted.Length - 3);
        }

        return extracted.Trim();
    }
}
