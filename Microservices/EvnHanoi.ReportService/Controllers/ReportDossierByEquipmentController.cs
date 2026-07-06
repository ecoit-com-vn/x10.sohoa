using EvnHanoi.ReportService.Core.Interfaces;
using EvnHanoi.ReportService.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.ReportService.Controllers;

[ApiController]
[Route("api/v1/reports/dossier-by-equipment")]
public class ReportDossierByEquipmentController : ReportDossierControllerBase
{
    public ReportDossierByEquipmentController(
        IReportDossierSearchService searchService,
        IReportDossierDetailRepository detailRepository,
        IReportDossierRepository repository)
        : base(searchService, detailRepository, repository)
    {
    }

    protected override ReportDossierKind Kind => ReportDossierKind.Equipment;
    protected override string ReportTitle => "Bao_cao_thong_ke_ho_so_thiet_bi_theo_thiet_bi";
    protected override string DimensionColumnLabel => "Thiết bị";

    [HttpGet("lookups/equipments")]
    public Task<IActionResult> GetEquipments([FromQuery] long? unitId) => GetEquipmentLookups(unitId);
}
