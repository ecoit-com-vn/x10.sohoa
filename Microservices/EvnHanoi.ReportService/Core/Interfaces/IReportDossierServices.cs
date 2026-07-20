using EvnHanoi.ReportService.Core.DTOs;
using EvnHanoi.ReportService.Core.Models;

namespace EvnHanoi.ReportService.Core.Interfaces;

public interface IReportDossierRepository
{
    Task<IEnumerable<ReportDossierLookupItem>> GetOrganizationUnitsAsync(bool isAdmin, long? userUnitId);
    Task<IEnumerable<ReportDossierLookupItem>> GetGridTypesAsync(long? unitScopeRoot);
    Task<IEnumerable<ReportDossierLookupItem>> GetEquipmentTypesAsync(long? unitScopeRoot);
    Task<IEnumerable<ReportDossierLookupItem>> GetEquipmentsAsync(long? unitScopeRoot, long? filterUnitId);
    Task<IEnumerable<ReportDossierLookupItem>> GetInfrastructuresAsync(long? unitScopeRoot, long? filterUnitId, int infraTypeId);
    Task<IEnumerable<ReportDossierBhsColumn>> GetBhsColumnsAsync();
    Task<Dictionary<long, string>> GetUnitNamesAsync(IEnumerable<long> unitIds);
    Task<IReadOnlyList<long>> ResolveUnitScopeIdsAsync(long unitId);
    Task<IReadOnlyList<string>> ResolveInfrastructureScopeIdsAsync(int infraTypeId, IReadOnlyList<long>? unitScopeIds);

    // Báo cáo thống kê hồ sơ nhập liệu theo năm
    Task<IEnumerable<int>> GetAvailableYearsAsync();
    Task<IEnumerable<DossierByYearChartStatDto>> GetDossierByYearChartStatsAsync(DossierByYearFilterDto filter, bool isAdmin, long? userUnitId);
    Task<IEnumerable<DossierByYearRatioStatDto>> GetDossierByYearRatioStatsAsync(DossierByYearFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByYearListAsync(DossierByYearFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsStationGridResponseDto> GetDossierByYearStationGridAsync(DossierByYearFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ nhập liệu theo tháng
    Task<IEnumerable<ReportMonthLookupDto>> GetAvailableMonthsAsync();
    Task<IEnumerable<DossierByMonthChartStatDto>> GetDossierByMonthChartStatsAsync(DossierByMonthFilterDto filter, bool isAdmin, long? userUnitId);
    Task<IEnumerable<DossierByMonthRatioStatDto>> GetDossierByMonthRatioStatsAsync(DossierByMonthFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByMonthListAsync(DossierByMonthFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsStationGridResponseDto> GetDossierByMonthStationGridAsync(DossierByMonthFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ nhập liệu theo lưới điện áp
    Task<IEnumerable<DossierByVoltageGridChartStatDto>> GetDossierByVoltageGridChartStatsAsync(DossierByVoltageGridFilterDto filter, bool isAdmin, long? userUnitId);
    Task<IEnumerable<DossierByVoltageGridRatioStatDto>> GetDossierByVoltageGridRatioStatsAsync(DossierByVoltageGridFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByVoltageGridListAsync(DossierByVoltageGridFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsStationGridResponseDto> GetDossierByVoltageGridStationGridAsync(DossierByVoltageGridFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ nhập liệu theo loại thiết bị
    Task<IEnumerable<DossierByEquipmentTypeChartStatDto>> GetDossierByEquipmentTypeChartStatsAsync(DossierByEquipmentTypeFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByEquipmentTypeListAsync(DossierByEquipmentTypeFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsEquipmentTypeGridResponseDto> GetDossierByEquipmentTypeGridAsync(DossierByEquipmentTypeFilterDto filter, bool isAdmin, long? userUnitId);
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
