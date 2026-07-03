namespace EvnHanoi.Infrastructure.Security;

/// <summary>
/// Quyền cấp cao hơn được coi là đủ cho quyền cấp thấp hơn (vd. MANAGE → VIEW).
/// </summary>
public static class PermissionImplicationResolver
{
    private static readonly Dictionary<string, string[]> ImpliedBy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DOSSIER_VIEW"] = ["DOSSIER_MANAGE", "DOSSIER_CREATE", "DOSSIER_EDIT"],
        ["DOSSIER_EDIT"] = ["DOSSIER_MANAGE", "DOSSIER_CREATE"],
        ["DOSSIER_DIGITIZATION_VIEW"] = ["DOSSIER_DIGITIZATION_MANAGE", "DOSSIER_DIGITIZATION_CREATE", "DOSSIER_DIGITIZATION_EDIT"],
        ["DOSSIER_DIGITIZATION_EDIT"] = ["DOSSIER_DIGITIZATION_MANAGE", "DOSSIER_DIGITIZATION_CREATE"],
    };

    public static IReadOnlyList<string> GetImpliedAlternates(string requiredPermission)
    {
        if (string.IsNullOrWhiteSpace(requiredPermission))
            return Array.Empty<string>();

        return ImpliedBy.TryGetValue(requiredPermission.Trim(), out var alternates)
            ? alternates
            : Array.Empty<string>();
    }
}
