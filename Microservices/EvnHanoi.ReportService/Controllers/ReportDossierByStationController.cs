using EvnHanoi.ReportService.Core.Interfaces;
using EvnHanoi.ReportService.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.ReportService.Controllers;

[ApiController]
[Route("api/v1/reports/dossier-by-station")]
public class ReportDossierByStationController : ReportDossierControllerBase
{
    public ReportDossierByStationController(
        IReportDossierSearchService searchService,
        IReportDossierDetailRepository detailRepository,
        IReportDossierRepository repository)
        : base(searchService, detailRepository, repository)
    {
    }

    protected override ReportDossierKind Kind => ReportDossierKind.Station;
    protected override string ReportTitle => "Bao_cao_ho_so_thiet_bi_theo_tram";
    protected override string DimensionColumnLabel => "Trạm / Đường dây";

    [HttpGet("lookups/stations")]
    public Task<IActionResult> GetStations([FromQuery] long? unitId) => GetInfrastructureLookups(unitId, 1);
}
