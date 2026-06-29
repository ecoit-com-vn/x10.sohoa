namespace EvnHanoi.NotificationService.Models;

/// <summary>Phạm vi menu FE khi gọi API search hồ sơ.</summary>
public static class DossierMenuScopes
{
    public const string Creator = "creator";
    public const string Approver = "approver";

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is Creator or Approver ? normalized : null;
    }

    public static bool IsCreator(string? scope) =>
        string.Equals(Normalize(scope), Creator, StringComparison.Ordinal);

    public static bool IsApprover(string? scope) =>
        string.Equals(Normalize(scope), Approver, StringComparison.Ordinal);
}
