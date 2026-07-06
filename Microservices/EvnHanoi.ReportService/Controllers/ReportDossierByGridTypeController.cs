using EvnHanoi.ReportService.Core.Interfaces;
using EvnHanoi.ReportService.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.ReportService.Controllers;

[ApiController]
[Route("api/v1/reports/dossier-by-grid-type")]
public class ReportDossierByGridTypeController : ReportDossierControllerBase
{
    public ReportDossierByGridTypeController(
        IReportDossierSearchService searchService,
        IReportDossierDetailRepository detailRepository,
        IReportDossierRepository repository)
        : base(searchService, detailRepository, repository)
    {
    }

    protected override ReportDossierKind Kind => ReportDossierKind.GridType;
    protected override string ReportTitle => "Bao_cao_thong_ke_ho_so_thiet_bi_theo_loai_luoi_dien";
    protected override string DimensionColumnLabel => "Loại lưới điện";

    [HttpGet("lookups/grid-types")]
    public Task<IActionResult> GetGridTypes([FromQuery] long? unitId) => GetGridTypeLookups(unitId);
}
