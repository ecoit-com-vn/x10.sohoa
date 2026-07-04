using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Repositories;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.NotificationService.Services;

public class DocumentIndexer : IDocumentIndexer
{
    private static readonly string[] SummaryFieldCandidates =
    [
        "trich_yeu", "trichyeu", "summary", "noi_dung_trich_yeu", "abstract", "mo_ta", "description"
    ];

    private readonly ElasticsearchClient _elasticClient;
    private readonly IDocumentEnrichmentRepository _enrichmentRepository;
    private readonly IMinioOcrTextReader _ocrTextReader;
    private readonly ILogger<DocumentIndexer> _logger;

    public DocumentIndexer(
        ElasticsearchClient elasticClient,
        IDocumentEnrichmentRepository enrichmentRepository,
        IMinioOcrTextReader ocrTextReader,
        ILogger<DocumentIndexer> logger)
    {
        _elasticClient = elasticClient;
        _enrichmentRepository = enrichmentRepository;
        _ocrTextReader = ocrTextReader;
        _logger = logger;
    }

    public async Task<bool> IndexByVersionIdAsync(
        string documentVersionId,
        string? bucketNameOverride,
        string? filePathOverride,
        int totalPagesHint,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = DossierIndexIdNormalizer.Normalize(documentVersionId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            _logger.LogWarning("Skip document indexing with empty version id.");
            return false;
        }

        var data = await _enrichmentRepository.GetByVersionIdAsync(normalizedId);
        if (data is null || data.DocumentIsDeleted)
        {
            _logger.LogWarning(
                "Document version {VersionId} not found or deleted — removing from {IndexName}.",
                normalizedId,
                DocumentTextMessaging.IndexName);
            return await DeleteByVersionIdAsync(normalizedId, cancellationToken);
        }

        var bucketName = bucketNameOverride ?? data.BucketName;
        var filePath = filePathOverride ?? data.FilePath;
        if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning(
                "Document version {VersionId} thiếu bucket/filePath — bỏ qua index.",
                normalizedId);
            return false;
        }

        var fullText = await _ocrTextReader.ReadConcatenatedMarkdownAsync(
            bucketName,
            filePath,
            totalPagesHint,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(fullText))
        {
            _logger.LogWarning(
                "Không đọc được nội dung OCR cho version {VersionId} — bỏ qua index.",
                normalizedId);
            return false;
        }

        var equipmentNames = new List<string>();
        if (!string.IsNullOrWhiteSpace(data.DossierId))
        {
            equipmentNames = (await _enrichmentRepository.GetEquipmentNamesByDossierIdAsync(data.DossierId))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var document = new DocumentEsDocument
        {
            Id = normalizedId,
            DocumentId = DossierIndexIdNormalizer.Normalize(data.DocumentId),
            DocumentVersionId = normalizedId,
            DocumentName = data.DocumentName,
            FullText = fullText,
            MimeType = data.MimeType,
            FilePath = filePath,
            BucketName = bucketName,
            DossierId = DossierIndexIdNormalizer.NormalizeOrNull(data.DossierId),
            DossierTitle = BuildDossierTitle(data),
            InfrastructureId = DossierIndexIdNormalizer.NormalizeOrNull(data.InfrastructureId),
            InfrastructureName = data.InfrastructureName,
            InfrastructureCode = data.InfrastructureCode,
            UnitId = data.UnitId,
            DossierTypeId = DossierIndexIdNormalizer.NormalizeOrNull(data.DossierTypeId),
            DossierTypeName = data.DossierTypeName,
            DocumentTypeId = DossierIndexIdNormalizer.NormalizeOrNull(data.DocumentTypeId),
            DocumentTypeName = data.DocumentTypeName,
            StatusId = data.StatusId,
            StatusCode = data.StatusCode,
            PublishStatusId = data.PublishStatusId,
            PublishStatusCode = data.PublishStatusCode,
            EquipmentNames = equipmentNames,
            ExtractionSummary = ExtractSummary(data.MergedDataJson),
            OcrCompletedAt = data.OcrCompletedAt ?? DateTime.UtcNow,
            IndexedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        var response = await _elasticClient.IndexAsync(
            document,
            idx => idx
                .Index(DocumentTextMessaging.IndexName)
                .Id(normalizedId)
                .Refresh(Refresh.WaitFor),
            cancellationToken);

        if (!response.IsValidResponse &&
            response.ElasticsearchServerError?.Error?.Type == "index_not_found_exception")
        {
            _logger.LogWarning("Index {IndexName} missing, creating now...", DocumentTextMessaging.IndexName);
            await DocumentIndexSetup.EnsureIndexExistsAsync(_elasticClient, _logger, cancellationToken);
            response = await _elasticClient.IndexAsync(
                document,
                idx => idx
                    .Index(DocumentTextMessaging.IndexName)
                    .Id(normalizedId)
                    .Refresh(Refresh.WaitFor),
                cancellationToken);
        }

        if (!response.IsValidResponse)
        {
            _logger.LogError(
                "Failed to index document version {VersionId}: {Error}",
                normalizedId,
                response.DebugInformation);
            return false;
        }

        _logger.LogInformation(
            "Indexed document version {VersionId} ({DocumentName}) to {IndexName}.",
            normalizedId,
            data.DocumentName,
            DocumentTextMessaging.IndexName);
        return true;
    }

    public async Task<bool> DeleteByVersionIdAsync(string documentVersionId, CancellationToken cancellationToken = default)
    {
        var normalizedId = DossierIndexIdNormalizer.Normalize(documentVersionId);
        if (string.IsNullOrEmpty(normalizedId))
            return false;

        foreach (var variant in DossierIndexIdNormalizer.GetGuidTermVariants(normalizedId))
        {
            await _elasticClient.DeleteAsync(
                DocumentTextMessaging.IndexName,
                variant,
                cancellationToken);
        }

        await _elasticClient.Indices.RefreshAsync(DocumentTextMessaging.IndexName, cancellationToken);
        return true;
    }

    private static string? BuildDossierTitle(DocumentEnrichmentData data)
    {
        if (!string.IsNullOrWhiteSpace(data.InfrastructureName) && !string.IsNullOrWhiteSpace(data.DossierTypeName))
            return $"{data.InfrastructureName} — {data.DossierTypeName}";
        if (!string.IsNullOrWhiteSpace(data.InfrastructureName))
            return data.InfrastructureName;
        return data.DossierTypeName;
    }

    private static string? ExtractSummary(string? mergedDataJson)
    {
        if (string.IsNullOrWhiteSpace(mergedDataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(mergedDataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!SummaryFieldCandidates.Any(candidate =>
                        property.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var value = ReadJsonValue(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? ReadJsonValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => element.GetRawText()
        };
}
