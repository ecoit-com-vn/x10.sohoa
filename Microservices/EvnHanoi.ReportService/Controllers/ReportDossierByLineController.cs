using EvnHanoi.ReportService.Core.Interfaces;
using EvnHanoi.ReportService.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.ReportService.Controllers;

[ApiController]
[Route("api/v1/reports/dossier-by-line")]
public class ReportDossierByLineController : ReportDossierControllerBase
{
    public ReportDossierByLineController(
        IReportDossierSearchService searchService,
        IReportDossierDetailRepository detailRepository,
        IReportDossierRepository repository)
        : base(searchService, detailRepository, repository)
    {
    }

    protected override ReportDossierKind Kind => ReportDossierKind.Line;
    protected override string ReportTitle => "Bao_cao_ho_so_thiet_bi_theo_duong_day";
    protected override string DimensionColumnLabel => "Trạm / Đường dây";

    [HttpGet("lookups/lines")]
    public Task<IActionResult> GetLines([FromQuery] long? unitId) => GetInfrastructureLookups(unitId, 2);
}
