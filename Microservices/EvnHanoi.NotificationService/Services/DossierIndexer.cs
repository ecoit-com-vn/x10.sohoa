using Elastic.Clients.Elasticsearch;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.NotificationService.Services;

public class DossierIndexer : IDossierIndexer
{
    private const string BhsCatalogCacheKey = "dossier-index:bhs-catalogs";

    private readonly ElasticsearchClient _elasticClient;
    private readonly IDossierEnrichmentRepository _enrichmentRepository;
    private readonly IDossierDocumentBuilder _documentBuilder;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<DossierIndexer> _logger;

    public DossierIndexer(
        ElasticsearchClient elasticClient,
        IDossierEnrichmentRepository enrichmentRepository,
        IDossierDocumentBuilder documentBuilder,
        IMemoryCache memoryCache,
        ILogger<DossierIndexer> logger)
    {
        _elasticClient = elasticClient;
        _enrichmentRepository = enrichmentRepository;
        _documentBuilder = documentBuilder;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<bool> IndexByIdAsync(string dossierId, CancellationToken cancellationToken = default)
    {
        var normalizedId = DossierIndexIdNormalizer.Normalize(dossierId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            _logger.LogWarning("Skip indexing dossier with empty id.");
            return false;
        }

        var data = await LoadEnrichmentWithWorkflowRetryAsync(normalizedId, cancellationToken);
        if (data is null)
        {
            _logger.LogWarning(
                "Dossier {DossierId} not found in Oracle — removing orphan document from {IndexName}.",
                normalizedId,
                DossierMessaging.IndexName);
            return await DeleteByIdAsync(normalizedId, cancellationToken);
        }

        if (data.IsDeleted)
        {
            _logger.LogInformation(
                "Dossier {DossierId} is soft-deleted in Oracle — removing from {IndexName}.",
                normalizedId,
                DossierMessaging.IndexName);
            return await DeleteByIdAsync(normalizedId, cancellationToken);
        }

        var bhsCatalogs = await GetBhsCatalogsCachedAsync();
        var equipments = await _enrichmentRepository.GetEquipmentsAsync(normalizedId);
        var document = _documentBuilder.Build(data, bhsCatalogs, equipments);
        document.Id = normalizedId;

        var response = await _elasticClient.IndexAsync(
            document,
            idx => idx
                .Index(DossierMessaging.IndexName)
                .Id(normalizedId)
                .Refresh(Refresh.WaitFor),
            cancellationToken);

        if (!response.IsValidResponse &&
            response.ElasticsearchServerError?.Error?.Type == "index_not_found_exception")
        {
            _logger.LogWarning("Index {IndexName} missing, creating now...", DossierMessaging.IndexName);
            await DossierIndexSetup.EnsureIndexExistsAsync(_elasticClient, _logger, cancellationToken);
            response = await _elasticClient.IndexAsync(
                document,
                idx => idx
                    .Index(DossierMessaging.IndexName)
                    .Id(normalizedId)
                    .Refresh(Refresh.WaitFor),
                cancellationToken);
        }

        if (!response.IsValidResponse)
        {
            _logger.LogError(
                "Failed to index dossier {DossierId}: {Error}",
                normalizedId,
                response.DebugInformation);
            return false;
        }

        _logger.LogInformation(
            "Indexed dossier {DossierId} to {IndexName} (status={Status}, isDeleted={IsDeleted}).",
            normalizedId,
            DossierMessaging.IndexName,
            document.Status,
            document.IsDeleted);
        return true;
    }

    public async Task<bool> DeleteByIdAsync(string dossierId, CancellationToken cancellationToken = default)
    {
        var normalizedId = DossierIndexIdNormalizer.Normalize(dossierId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            _logger.LogWarning("Skip deleting dossier with empty id from ES.");
            return false;
        }

        var allDeleted = true;
        foreach (var documentId in DossierIndexIdNormalizer.GetGuidTermVariants(normalizedId))
        {
            if (!await TryDeleteDocumentByIdAsync(documentId, cancellationToken))
                allDeleted = false;
        }

        return allDeleted;
    }

    public async Task<int> PurgeLegacyDocumentIdsAsync(CancellationToken cancellationToken = default)
    {
        var dossierIds = (await _enrichmentRepository.GetAllIdsAsync()).ToList();
        var removed = 0;

        foreach (var rawId in dossierIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var canonicalId = DossierIndexIdNormalizer.Normalize(rawId);
            foreach (var variantId in DossierIndexIdNormalizer.GetGuidTermVariants(canonicalId))
            {
                if (string.Equals(variantId, canonicalId, StringComparison.Ordinal))
                    continue;

                var response = await _elasticClient.DeleteAsync<DossierEsDocument>(
                    variantId,
                    d => d
                        .Index(DossierMessaging.IndexName)
                        .Refresh(Refresh.False),
                    cancellationToken);

                if (response.Result == Result.Deleted)
                {
                    removed++;
                    _logger.LogInformation(
                        "Purged legacy dossier ES document _id={LegacyId} (canonical={CanonicalId}).",
                        variantId,
                        canonicalId);
                }
            }
        }

        _logger.LogInformation(
            "Legacy dossier document id purge finished — removed {Removed} orphan document(s).",
            removed);
        return removed;
    }

    private async Task<bool> TryDeleteDocumentByIdAsync(string documentId, CancellationToken cancellationToken)
    {
        var response = await _elasticClient.DeleteAsync<DossierEsDocument>(
            documentId,
            d => d
                .Index(DossierMessaging.IndexName)
                .Refresh(Refresh.WaitFor),
            cancellationToken);

        if (response.Result == Result.NotFound)
            return true;

        if (!response.IsValidResponse)
        {
            _logger.LogError(
                "Failed to delete dossier {DossierId} from {IndexName}: {Error}",
                documentId,
                DossierMessaging.IndexName,
                response.DebugInformation);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Sau move/submit, reindex đồng bộ có thể chạy ngay khi WORKFLOWTASKS Pending chưa visible — retry ngắn.
    /// </summary>
    private async Task<DossierEnrichmentData?> LoadEnrichmentWithWorkflowRetryAsync(
        string dossierId,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        const int retryDelayMs = 150;

        DossierEnrichmentData? data = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            data = await _enrichmentRepository.GetByIdAsync(dossierId);
            if (data is null || !NeedsWorkflowInboxRetry(data))
                return data;

            if (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    "Dossier {DossierId}: instance Running nhưng thiếu inbox fields (attempt {Attempt}/{Max}) — retry sau {DelayMs}ms.",
                    dossierId,
                    attempt,
                    maxAttempts,
                    retryDelayMs);
                await Task.Delay(retryDelayMs, cancellationToken);
            }
        }

        if (data is not null && NeedsWorkflowInboxRetry(data))
        {
            _logger.LogWarning(
                "Dossier {DossierId}: vẫn thiếu pendingAssigneeUserId/workflowParticipantUserIds sau {Max} lần enrich.",
                dossierId,
                maxAttempts);
        }

        return data;
    }

    private static bool NeedsWorkflowInboxRetry(DossierEnrichmentData data)
    {
        if (!string.Equals(data.WorkflowInstanceStatus, "Running", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(data.PendingAssigneeUserId))
            return false;

        if (data.PendingAssignedRoles.Any(r => !string.IsNullOrWhiteSpace(r)))
            return false;

        return data.WorkflowParticipantUserIds.Count == 0;
    }

    private async Task<IReadOnlyList<BhsCatalogDefinition>> GetBhsCatalogsCachedAsync()
    {
        if (_memoryCache.TryGetValue(BhsCatalogCacheKey, out IReadOnlyList<BhsCatalogDefinition>? cached) &&
            cached is not null)
            return cached;

        var catalogs = (await _enrichmentRepository.GetBhsCatalogDefinitionsAsync()).ToList();
        _memoryCache.Set(BhsCatalogCacheKey, catalogs, TimeSpan.FromMinutes(10));
        return catalogs;
    }
}
