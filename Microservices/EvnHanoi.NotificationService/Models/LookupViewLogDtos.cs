namespace EvnHanoi.NotificationService.Models;

public class RecordLookupViewRequest
{
    /// <summary>DOSSIER | DOCUMENT</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Id hồ sơ — luôn có, kể cả khi EntityType = DOCUMENT (biết tài liệu thuộc hồ sơ nào).</summary>
    public string DossierId { get; set; } = string.Empty;
}

public static class LookupViewEntityTypes
{
    public const string Dossier = "DOSSIER";
    public const string Document = "DOCUMENT";

    public static bool IsValid(string? value) =>
        value == Dossier || value == Document;
}
