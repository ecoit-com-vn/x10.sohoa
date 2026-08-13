using DocumentFormat.OpenXml.Wordprocessing;
using EvnHanoi.ReportService.Core.DTOs;
using EvnHanoi.ReportService.Core.Models;

namespace EvnHanoi.ReportService.Core.Interfaces;

public interface IReportDossierRepository
{
    Task<IEnumerable<ReportDossierLookupItem>> GetOrganizationUnitsAsync(bool isAdmin, long? userUnitId);
    Task<IEnumerable<ReportDossierLookupItem>> GetOrganizationUnitsWithStatusAsync(bool isAdmin, long? userUnitId, int isactive);
    Task<IEnumerable<ReportDossierLookupItem>> GetGridTypesAsync(long? unitScopeRoot);
    Task<IEnumerable<ReportDossierLookupItem>> GetEquipmentTypesAsync(long? unitScopeRoot);
    Task<IEnumerable<ReportDossierLookupItem>> GetDossierTypesAsync(long? unitScopeRoot);
    Task<IEnumerable<ReportDossierLookupItem>> GetDocumentTypesAsync();
    Task<IEnumerable<ReportDossierLookupItem>> GetShelvesAsync(long? unitScopeRoot, long? filterUnitId);
    Task<IEnumerable<ReportDossierLookupItem>> GetFloorsAsync(long? unitScopeRoot, long? filterUnitId);
    Task<IReadOnlyList<ReportShelfFloorLookupDto>> GetShelfFloorsAsync(long? unitScopeRoot, long? filterUnitId);
    Task<IEnumerable<ReportDossierLookupItem>> GetBoxesAsync(long? unitScopeRoot, long? filterUnitId);
    Task<IEnumerable<ReportBoxsesDetailLookupItem>> GetBoxesDetailLookup(long? unitScopeRoot, long? filterUnitId);
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

    // Báo cáo thống kê tổng hợp hồ sơ nhập liệu (filter khoảng ngày, không theo năm)
    Task<IEnumerable<DossierGeneralInputChartStatDto>> GetDossierGeneralInputChartStatsAsync(DossierGeneralInputFilterDto filter, bool isAdmin, long? userUnitId);
    Task<IEnumerable<DossierGeneralInputRatioStatDto>> GetDossierGeneralInputRatioStatsAsync(DossierGeneralInputFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierGeneralInputListAsync(DossierGeneralInputFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsStationGridResponseDto> GetDossierGeneralInputStationGridAsync(DossierGeneralInputFilterDto filter, bool isAdmin, long? userUnitId);

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

    // Báo cáo thống kê hồ sơ theo kệ lưu trữ (xuất bản)
    Task<IEnumerable<DossierByShelfChartStatDto>> GetDossierByShelfChartStatsAsync(DossierByShelfFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByShelfListAsync(DossierByShelfFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsShelfGridResponseDto> GetDossierByShelfGridAsync(DossierByShelfFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ theo hộp lưu trữ (xuất bản)
    Task<IEnumerable<DossierByBoxChartStatDto>> GetDossierByBoxChartStatsAsync(DossierByBoxFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByBoxListAsync(DossierByBoxFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsBoxGridResponseDto> GetDossierByBoxGridAsync(DossierByBoxFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ theo tầng lưu trữ (xuất bản)
    Task<IEnumerable<DossierByFloorChartStatDto>> GetDossierByFloorChartStatsAsync(DossierByFloorFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByFloorListAsync(DossierByFloorFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsFloorGridResponseDto> GetDossierByFloorGridAsync(DossierByFloorFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ theo loại hồ sơ (xuất bản)
    Task<IEnumerable<DossierByDossierTypeChartStatDto>> GetDossierByDossierTypeChartStatsAsync(DossierByDossierTypeFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByDossierTypeListAsync(DossierByDossierTypeFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierTypeGridResponseDto> GetDossierByDossierTypeGridAsync(DossierByDossierTypeFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ theo loại văn bản (xuất bản)
    Task<IEnumerable<DossierByDocumentTypeChartStatDto>> GetDossierByDocumentTypeChartStatsAsync(DossierByDocumentTypeFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDocumentListResponseDto> GetDossierByDocumentTypeDocumentListAsync(DossierByDocumentTypeFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDocumentTypeGridResponseDto> GetDossierByDocumentTypeGridAsync(DossierByDocumentTypeFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ theo trạm (xuất bản)
    Task<DossierByStationSummaryStatsDto> GetDossierByStationSummaryStatsAsync(DossierByStationFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByStationListAsync(DossierByStationFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsStationGridResponseDto> GetDossierByStationStationGridAsync(DossierByStationFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ theo đường dây (xuất bản)
    Task<DossierByLineSummaryStatsDto> GetDossierByLineSummaryStatsAsync(DossierByLineFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByLineListAsync(DossierByLineFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsStationGridResponseDto> GetDossierByLineLineGridAsync(DossierByLineFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ theo năm vận hành (xuất bản)
    Task<IEnumerable<int>> GetAvailableOperationYearsAsync();
    Task<DossierByOperationYearSummaryStatsDto> GetDossierByOperationYearSummaryStatsAsync(DossierByOperationYearFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByOperationYearListAsync(DossierByOperationYearFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsStationGridResponseDto> GetDossierByOperationYearStationGridAsync(DossierByOperationYearFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ thiết bị theo thời gian vận hành (xuất bản)
    Task<DossierByOperationTimeSummaryStatsDto> GetDossierByOperationTimeSummaryStatsAsync(DossierByOperationTimeFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByOperationTimeListAsync(DossierByOperationTimeFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsStationGridResponseDto> GetDossierByOperationTimeStationGridAsync(DossierByOperationTimeFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ thiết bị theo năm sản xuất (xuất bản)
    Task<IEnumerable<int>> GetAvailableManufactureYearsAsync();
    Task<IEnumerable<DossierByManufactureYearChartStatDto>> GetDossierByManufactureYearChartStatsAsync(DossierByManufactureYearFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByManufactureYearListAsync(DossierByManufactureYearFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsEquipmentGridResponseDto> GetDossierByManufactureYearEquipmentGridAsync(DossierByManufactureYearFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ thiết bị theo tình trạng thiết bị (xuất bản)
    Task<IEnumerable<ReportDossierLookupItem>> GetEquipmentStatusesAsync();
    Task<IEnumerable<DossierByEquipmentStatusChartStatDto>> GetDossierByEquipmentStatusChartStatsAsync(DossierByEquipmentStatusFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByEquipmentStatusListAsync(DossierByEquipmentStatusFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsEquipmentStatusGridResponseDto> GetDossierByEquipmentStatusEquipmentGridAsync(DossierByEquipmentStatusFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê tổng hợp hồ sơ được tra cứu nhiều nhất (LOOKUP_VIEW_LOGS)
    Task<DossierMostViewedSummaryStatsDto> GetDossierMostViewedSummaryStatsAsync(DossierMostViewedFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierViewGridResponseDto> GetDossierMostViewedGridAsync(DossierMostViewedFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo thống kê hồ sơ nhập liệu theo phân bổ hồ sơ
    Task<IEnumerable<ReportInputUserLookupDto>> GetInputUsersAsync(bool isAdmin, long? userUnitId);
    Task<IEnumerable<DossierByAllocationChartStatDto>> GetDossierByAllocationChartStatsAsync(DossierByAllocationFilterDto filter, bool isAdmin, long? userUnitId);
    Task<IEnumerable<DossierByAllocationRatioStatDto>> GetDossierByAllocationRatioStatsAsync(DossierByAllocationFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByAllocationListAsync(DossierByAllocationFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsCreatorGridResponseDto> GetDossierByAllocationCreatorGridAsync(DossierByAllocationFilterDto filter, bool isAdmin, long? userUnitId);

    // Báo cáo hồ sơ thiết bị theo cán bộ nhập liệu (không giới hạn KIND_ID)
    Task<IEnumerable<DossierByAllocationChartStatDto>> GetDossierByInputOfficerChartStatsAsync(DossierByInputOfficerFilterDto filter, bool isAdmin, long? userUnitId);
    Task<IEnumerable<DossierByAllocationRatioStatDto>> GetDossierByInputOfficerRatioStatsAsync(DossierByInputOfficerFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsDossierListResponseDto> GetDossierByInputOfficerListAsync(DossierByInputOfficerFilterDto filter, bool isAdmin, long? userUnitId);
    Task<ReportStatisticsCreatorGridResponseDto> GetDossierByInputOfficerCreatorGridAsync(DossierByInputOfficerFilterDto filter, bool isAdmin, long? userUnitId);
    Task<IEnumerable<ReportInputUserLookupDto>> GetDossierByInputOfficerUsersAsync(bool isAdmin, long? userUnitId);
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
