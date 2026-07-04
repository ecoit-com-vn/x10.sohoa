namespace EvnHanoi.NotificationService.Models;

public static class DocumentSearchConstants
{
    public const int ApprovedStatusId = 6;
    public const int PublishedStatusId = 2;
}

public class DocumentSearchFilterDto
{
    public string? Keyword { get; set; }
    public long? UnitId { get; set; }
    public IReadOnlyList<long>? UnitScopeIds { get; set; }
    public bool IsAdmin { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    /// <summary>newest | oldest | relevance</summary>
    public string Sort { get; set; } = "newest";
}

public class DocumentSearchItemDto
{
    public string DocumentVersionId { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string? Highlight { get; set; }
    public string? MimeType { get; set; }
    public string? DossierId { get; set; }
    public string? DossierTitle { get; set; }
    public string? InfrastructureName { get; set; }
    public string? DossierTypeName { get; set; }
    public string? DocumentTypeName { get; set; }
    public IReadOnlyList<string> EquipmentNames { get; set; } = Array.Empty<string>();
    public DateTime? IndexedAt { get; set; }
}

public class DocumentSearchDetailDto
{
    public string DocumentVersionId { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public string? FilePath { get; set; }
    public string? BucketName { get; set; }
    public string? DossierId { get; set; }
    public string? DossierTitle { get; set; }
    public string? InfrastructureId { get; set; }
    public string? InfrastructureName { get; set; }
    public string? InfrastructureCode { get; set; }
    public string? DossierTypeId { get; set; }
    public string? DossierTypeName { get; set; }
    public string? DocumentTypeId { get; set; }
    public string? DocumentTypeName { get; set; }
    public IReadOnlyList<string> EquipmentNames { get; set; } = Array.Empty<string>();
    public string? ExtractionSummary { get; set; }
    public string? MergedDataJson { get; set; }
    public DateTime? OcrCompletedAt { get; set; }
    public DateTime? IndexedAt { get; set; }
}
