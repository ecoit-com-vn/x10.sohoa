using EvnHanoi.ReportService.Core.Models;

namespace EvnHanoi.ReportService.Core.Interfaces;

public interface IReportDossierRepository
{
    Task<IEnumerable<ReportDossierLookupItem>> GetOrganizationUnitsAsync(bool isAdmin, long? userUnitId);
    Task<IEnumerable<ReportDossierLookupItem>> GetGridTypesAsync(long? unitScopeRoot);
    Task<IEnumerable<ReportDossierLookupItem>> GetEquipmentsAsync(long? unitScopeRoot, long? filterUnitId);
    Task<IEnumerable<ReportDossierLookupItem>> GetInfrastructuresAsync(long? unitScopeRoot, long? filterUnitId, int infraTypeId);
    Task<IEnumerable<ReportDossierBhsColumn>> GetBhsColumnsAsync();
    Task<Dictionary<long, string>> GetUnitNamesAsync(IEnumerable<long> unitIds);
    Task<IReadOnlyList<long>> ResolveUnitScopeIdsAsync(long unitId);
    Task<IReadOnlyList<string>> ResolveInfrastructureScopeIdsAsync(int infraTypeId, IReadOnlyList<long>? unitScopeIds);
}

public interface IReportDossierSearchService
{
    Task<ReportDossierSearchResponse> SearchAsync(ReportDossierSearchRequest request, CancellationToken cancellationToken = default);
}

public interface IReportDossierDetailRepository
{
    Task<bool> IsPublishedDossierAccessibleAsync(Guid dossierId, long? unitId);
    Task<ReportDossierDetailDto?> GetPublishedDetailAsync(Guid id);
    Task<(IEnumerable<ReportDocumentListItemDto> Items, int TotalCount)> GetDocumentsAsync(Guid dossierId, ReportDocumentFilterDto filter);
    Task<ReportDownloadTokenResponse?> CreateDocumentDownloadTokenAsync(Guid dossierId, Guid versionId, CancellationToken cancellationToken = default);
}
