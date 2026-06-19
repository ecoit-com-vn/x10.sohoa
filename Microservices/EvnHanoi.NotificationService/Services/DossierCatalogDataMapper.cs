using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Services;

/// <summary>
/// Map FormDataJson / formFields ES → catalogData hiển thị list (catalog BHS).
/// Key JSON trong FormDataJson thường là catalog.Code (EAV field key), cột UI dùng catalog.Name.
/// </summary>
public static class DossierCatalogDataMapper
{
    public static List<DossierCatalogFieldEs> BuildCatalogFields(
        IReadOnlyDictionary<string, object?> formData,
        IEnumerable<BhsCatalogDefinition> bhsCatalogs)
    {
        var catalogFields = new List<DossierCatalogFieldEs>();
        foreach (var catalog in bhsCatalogs.OrderBy(c => c.Priority))
        {
            if (!TryGetFormValue(formData, catalog, out var rawValue))
                continue;

            catalogFields.Add(new DossierCatalogFieldEs
            {
                CatalogCode = catalog.Code,
                CatalogName = catalog.Name,
                SortOrder = catalog.Priority,
                Value = FormatValue(rawValue)
            });
        }

        return catalogFields;
    }

    public static Dictionary<string, string> ToCatalogData(
        IEnumerable<DossierCatalogFieldEs> catalogFields,
        IEnumerable<DossierFormFieldEs> formFields,
        IEnumerable<BhsCatalogDefinition> bhsCatalogs)
    {
        if (catalogFields.Any())
        {
            return catalogFields
                .OrderBy(c => c.SortOrder)
                .GroupBy(c => c.CatalogName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
        }

        var formMap = formFields
            .Where(f => !string.IsNullOrWhiteSpace(f.FieldCode))
            .GroupBy(f => f.FieldCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().TextValue ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var catalog in bhsCatalogs.OrderBy(c => c.Priority))
        {
            if (TryGetTextValue(formMap, catalog, out var text) && !string.IsNullOrWhiteSpace(text))
                result[catalog.Name] = text;
        }

        return result;
    }

    public static bool TryGetFormValue(
        IReadOnlyDictionary<string, object?> formData,
        BhsCatalogDefinition catalog,
        out object? value)
    {
        if (formData.TryGetValue(catalog.Name, out value) && value is not null)
            return true;

        if (formData.TryGetValue(catalog.Code, out value) && value is not null)
            return true;

        foreach (var kv in formData)
        {
            if (string.Equals(kv.Key, catalog.Name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(kv.Key, catalog.Code, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return value is not null;
            }
        }

        value = null;
        return false;
    }

    private static bool TryGetTextValue(
        IReadOnlyDictionary<string, string> formMap,
        BhsCatalogDefinition catalog,
        out string text)
    {
        if (formMap.TryGetValue(catalog.Code, out text!) && !string.IsNullOrWhiteSpace(text))
            return true;

        if (formMap.TryGetValue(catalog.Name, out text!) && !string.IsNullOrWhiteSpace(text))
            return true;

        foreach (var kv in formMap)
        {
            if (string.Equals(kv.Key, catalog.Code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(kv.Key, catalog.Name, StringComparison.OrdinalIgnoreCase))
            {
                text = kv.Value;
                return !string.IsNullOrWhiteSpace(text);
            }
        }

        text = string.Empty;
        return false;
    }

    private static string FormatValue(object value)
    {
        if (value is System.Text.Json.JsonElement element)
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => element.GetString() ?? string.Empty,
                System.Text.Json.JsonValueKind.Number => element.GetRawText(),
                System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => element.GetBoolean().ToString(),
                _ => element.GetRawText()
            };

        return value.ToString() ?? string.Empty;
    }
}
