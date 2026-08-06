namespace EvnHanoi.NotificationService.Models;

public sealed class AuditLogRetentionStatusDto
{
    public int RetentionDays { get; init; }
    public DateTime NextCleanupAtUtc { get; init; }
    public int TotalIndices { get; init; }
    public long TotalDocuments { get; init; }
    public long TotalSizeBytes { get; init; }
    public IReadOnlyList<AuditLogRetentionIndexDto> Items { get; init; } = Array.Empty<AuditLogRetentionIndexDto>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
}

public sealed class AuditLogRetentionIndexDto
{
    public string IndexName { get; init; } = string.Empty;
    public DateOnly LogDate { get; init; }
    public long DocumentCount { get; init; }
    public long SizeBytes { get; init; }
    public DateTime EstimatedDeleteAtUtc { get; init; }
    public int RemainingDays { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class AuditLogIndexMetadata
{
    public string IndexName { get; init; } = string.Empty;
    public DateOnly LogDate { get; init; }
    public long DocumentCount { get; init; }
    public long SizeBytes { get; init; }
}
