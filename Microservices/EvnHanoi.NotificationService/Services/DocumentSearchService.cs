using System.Data;
using Dapper;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Repositories;

namespace EvnHanoi.NotificationService.Services;

public interface IDocumentSearchService
{
    Task<(IReadOnlyList<DocumentSearchItemDto> Items, int TotalCount)> SearchAsync(DocumentSearchFilterDto filter);
    Task<DocumentSearchDetailDto?> GetDetailAsync(string documentVersionId, DocumentSearchFilterDto scope);
}

public class DocumentSearchService : IDocumentSearchService
{
    private readonly IDocumentSearchRepository _searchRepository;
    private readonly IDocumentEnrichmentRepository _enrichmentRepository;
    private readonly IDbConnection _dbConnection;

    public DocumentSearchService(
        IDocumentSearchRepository searchRepository,
        IDocumentEnrichmentRepository enrichmentRepository,
        IDbConnection dbConnection)
    {
        _searchRepository = searchRepository;
        _enrichmentRepository = enrichmentRepository;
        _dbConnection = dbConnection;
    }

    public async Task<(IReadOnlyList<DocumentSearchItemDto> Items, int TotalCount)> SearchAsync(
        DocumentSearchFilterDto filter)
    {
        await ApplyUnitScopeAsync(filter);
        return await _searchRepository.SearchAsync(filter);
    }

    public async Task<DocumentSearchDetailDto?> GetDetailAsync(
        string documentVersionId,
        DocumentSearchFilterDto scope)
    {
        await ApplyUnitScopeAsync(scope);

        var doc = await _searchRepository.GetByVersionIdAsync(documentVersionId);
        if (doc is null || doc.IsDeleted)
            return null;

        if (!IsPublishedDocument(doc))
            return null;

        if (!IsWithinUnitScope(doc, scope))
            return null;

        var enrichment = await _enrichmentRepository.GetByVersionIdAsync(
            DossierIndexIdNormalizer.Normalize(documentVersionId));

        return new DocumentSearchDetailDto
        {
            DocumentVersionId = doc.DocumentVersionId,
            DocumentId = doc.DocumentId,
            DocumentName = doc.DocumentName,
            MimeType = doc.MimeType,
            FilePath = doc.FilePath,
            BucketName = doc.BucketName,
            DossierId = doc.DossierId,
            DossierTitle = doc.DossierTitle,
            InfrastructureId = doc.InfrastructureId,
            InfrastructureName = doc.InfrastructureName,
            InfrastructureCode = doc.InfrastructureCode,
            DossierTypeId = doc.DossierTypeId,
            DossierTypeName = doc.DossierTypeName,
            DocumentTypeId = doc.DocumentTypeId,
            DocumentTypeName = doc.DocumentTypeName,
            EquipmentNames = doc.EquipmentNames,
            ExtractionSummary = doc.ExtractionSummary,
            MergedDataJson = enrichment?.MergedDataJson,
            OcrCompletedAt = doc.OcrCompletedAt,
            IndexedAt = doc.IndexedAt
        };
    }

    private async Task ApplyUnitScopeAsync(DocumentSearchFilterDto filter)
    {
        if (filter.IsAdmin || !filter.UnitId.HasValue)
            return;

        if (_dbConnection.State != ConnectionState.Open)
            _dbConnection.Open();

        const string sql = @"
            SELECT Id
            FROM ORGANIZATION_UNIT
            START WITH Id = :StartUnitId
            CONNECT BY PRIOR Id = ParentId";

        var unitIds = await _dbConnection.QueryAsync<long>(sql, new { StartUnitId = filter.UnitId.Value });
        filter.UnitScopeIds = unitIds.Distinct().ToList();
    }

    private static bool IsPublishedDocument(DocumentEsDocument doc) =>
        doc.StatusId == DocumentSearchConstants.ApprovedStatusId &&
        doc.PublishStatusId == DocumentSearchConstants.PublishedStatusId;

    private static bool IsWithinUnitScope(DocumentEsDocument doc, DocumentSearchFilterDto scope)
    {
        if (scope.IsAdmin || scope.UnitScopeIds is null || scope.UnitScopeIds.Count == 0)
            return true;

        return doc.UnitId.HasValue && scope.UnitScopeIds.Contains(doc.UnitId.Value);
    }
}
