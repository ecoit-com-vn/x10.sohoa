namespace EvnHanoi.NotificationService.Models;

/// <summary>Phạm vi menu FE khi gọi API search hồ sơ.</summary>
public static class DossierMenuScopes
{
    public const string Creator = "creator";
    public const string Approver = "approver";
    public const string Publisher = "publisher";
    public const string EquipmentLookup = "equipment-lookup";
    public const string DocumentFulltext = "document-fulltext";
    public const string Report = "report";

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is Creator or Approver or Publisher or EquipmentLookup or DocumentFulltext or Report
            ? normalized
            : null;
    }

    public static bool IsCreator(string? scope) =>
        string.Equals(Normalize(scope), Creator, StringComparison.Ordinal);

    public static bool IsApprover(string? scope) =>
        string.Equals(Normalize(scope), Approver, StringComparison.Ordinal);

    public static bool IsPublisher(string? scope) =>
        string.Equals(Normalize(scope), Publisher, StringComparison.Ordinal);

    public static bool IsEquipmentLookup(string? scope) =>
        string.Equals(Normalize(scope), EquipmentLookup, StringComparison.Ordinal);

    public static bool IsDocumentFulltext(string? scope) =>
        string.Equals(Normalize(scope), DocumentFulltext, StringComparison.Ordinal);

    public static bool IsReport(string? scope) =>
        string.Equals(Normalize(scope), Report, StringComparison.Ordinal);
}
