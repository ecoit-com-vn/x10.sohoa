namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDocumentTextIndexNotifier
{
    Task PublishIndexAsync(
        Guid versionId,
        string? bucketName = null,
        string? filePath = null,
        int totalPages = 0,
        CancellationToken cancellationToken = default);

    Task PublishDeleteAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task PublishReindexDossierDocumentsAsync(Guid dossierId, CancellationToken cancellationToken = default);

    Task PublishDeleteDossierDocumentsAsync(Guid dossierId, CancellationToken cancellationToken = default);
}
