using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.EquipmentService.Infrastructure.Messaging;

public class DocumentTextIndexNotifier : IDocumentTextIndexNotifier
{
    private readonly IMessageProducer _messageProducer;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<DocumentTextIndexNotifier> _logger;

    public DocumentTextIndexNotifier(
        IMessageProducer messageProducer,
        IDocumentRepository documentRepository,
        ILogger<DocumentTextIndexNotifier> logger)
    {
        _messageProducer = messageProducer;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task PublishIndexAsync(
        Guid versionId,
        string? bucketName = null,
        string? filePath = null,
        int totalPages = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var evt = new DocumentTextIndexEvent(
                versionId.ToString(),
                bucketName ?? string.Empty,
                filePath ?? string.Empty,
                totalPages,
                DocumentTextIndexActions.Index,
                DateTime.UtcNow);

            await _messageProducer.PublishToExchangeAsync(
                evt,
                DigitizationTopicTopology.ExchangeName,
                DocumentTextMessaging.ReindexRoutingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không publish được sự kiện index tài liệu {VersionId}.", versionId);
            throw;
        }
    }

    public async Task PublishDeleteAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var evt = new DocumentTextIndexEvent(
                versionId.ToString(),
                string.Empty,
                string.Empty,
                0,
                DocumentTextIndexActions.Delete,
                DateTime.UtcNow);

            await _messageProducer.PublishToExchangeAsync(
                evt,
                DigitizationTopicTopology.ExchangeName,
                DocumentTextMessaging.ReindexRoutingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không publish được sự kiện xóa index tài liệu {VersionId}.", versionId);
            throw;
        }
    }

    public async Task PublishReindexDossierDocumentsAsync(Guid dossierId, CancellationToken cancellationToken = default)
    {
        var hints = await _documentRepository.GetOcrVersionIndexHintsByDossierIdAsync(dossierId);
        foreach (var hint in hints)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await PublishIndexAsync(
                hint.VersionId,
                hint.BucketName,
                hint.FilePath,
                hint.TotalPages,
                cancellationToken);
        }
    }

    public async Task PublishDeleteDossierDocumentsAsync(Guid dossierId, CancellationToken cancellationToken = default)
    {
        var versionIds = await _documentRepository.GetActiveVersionIdsByDossierIdAsync(dossierId);
        foreach (var versionId in versionIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await PublishDeleteAsync(versionId, cancellationToken);
        }
    }
}
