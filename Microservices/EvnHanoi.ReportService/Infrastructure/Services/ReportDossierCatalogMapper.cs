using EvnHanoi.ReportService.Core.Models;

namespace EvnHanoi.ReportService.Infrastructure.Services;

public static class ReportDossierCatalogMapper
{
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
}
