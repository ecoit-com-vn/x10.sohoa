using EvnHanoi.Infrastructure.Security;
using EvnHanoi.ReportService.Core.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.ReportService.Controllers;

public partial class ReportStatisticsController
{
    [HttpGet("dashboard/dossier-by-dossier-type/chart-stats")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDashboardDossierByDossierTypeChartStats()
    {
        if (!await HasDashboardPermissionAsync())
            return StatusCode(403, new { message = "Không có quyền truy cập Dashboard." });

        var scope = ResolveUserScope();
        var stats = await _dossierRepository.GetDossierByDossierTypeChartStatsAsync(
            new DossierByDossierTypeFilterDto(), scope.IsAdmin, scope.UnitId);
        return Ok(stats);
    }

    [HttpGet("dashboard/dossier-general-input/chart-stats")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDashboardDossierGeneralInputChartStats(
        [FromQuery] DossierGeneralInputFilterDto filter)
    {
        if (!await HasDashboardPermissionAsync())
            return StatusCode(403, new { message = "Không có quyền truy cập Dashboard." });

        var scope = ResolveUserScope();
        var stats = await _dossierRepository.GetDossierGeneralInputChartStatsAsync(
            filter, scope.IsAdmin, scope.UnitId);
        return Ok(stats);
    }

    [HttpGet("dashboard/dossier-most-viewed/summary-stats")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDashboardDossierMostViewedSummaryStats(
        [FromQuery] DossierMostViewedFilterDto filter)
    {
        if (!await HasDashboardPermissionAsync())
            return StatusCode(403, new { message = "Không có quyền truy cập Dashboard." });

        var scope = ResolveUserScope();
        var stats = await _dossierRepository.GetDossierMostViewedSummaryStatsAsync(
            filter, scope.IsAdmin, scope.UnitId);
        return Ok(stats);
    }
}
