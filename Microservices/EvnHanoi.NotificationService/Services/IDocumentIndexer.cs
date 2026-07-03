namespace EvnHanoi.NotificationService.Services;

public interface IDocumentIndexer
{
    Task<bool> IndexByVersionIdAsync(
        string documentVersionId,
        string? bucketNameOverride,
        string? filePathOverride,
        int totalPagesHint,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteByVersionIdAsync(string documentVersionId, CancellationToken cancellationToken = default);
}
