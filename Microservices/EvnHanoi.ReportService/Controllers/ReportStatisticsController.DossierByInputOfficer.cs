using System.Threading.Tasks;
using EvnHanoi.ReportService.Core.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.ReportService.Controllers
{
    public partial class ReportStatisticsController
    {
        [HttpGet("dossier-by-input-officer/input-users")]
        public async Task<IActionResult> GetDossierByInputOfficerUsers()
        {
            var scope = ResolveUserScope();
            return Ok(await _dossierRepository.GetDossierByInputOfficerUsersAsync(scope.IsAdmin, scope.UnitId));
        }

        [HttpGet("dossier-by-input-officer/chart-stats")]
        public async Task<IActionResult> GetDossierByInputOfficerChartStats([FromQuery] DossierByInputOfficerFilterDto filter)
        {
            var scope = ResolveUserScope();
            return Ok(await _dossierRepository.GetDossierByInputOfficerChartStatsAsync(filter, scope.IsAdmin, scope.UnitId));
        }

        [HttpGet("dossier-by-input-officer/ratio-stats")]
        public async Task<IActionResult> GetDossierByInputOfficerRatioStats([FromQuery] DossierByInputOfficerFilterDto filter)
        {
            var scope = ResolveUserScope();
            return Ok(await _dossierRepository.GetDossierByInputOfficerRatioStatsAsync(filter, scope.IsAdmin, scope.UnitId));
        }

        [HttpGet("dossier-by-input-officer/creator-grid")]
        public async Task<IActionResult> GetDossierByInputOfficerCreatorGrid([FromQuery] DossierByInputOfficerFilterDto filter)
        {
            var scope = ResolveUserScope();
            return Ok(await _dossierRepository.GetDossierByInputOfficerCreatorGridAsync(filter, scope.IsAdmin, scope.UnitId));
        }

        [HttpGet("dossier-by-input-officer/list")]
        public async Task<IActionResult> GetDossierByInputOfficerList([FromQuery] DossierByInputOfficerFilterDto filter)
        {
            var scope = ResolveUserScope();
            return Ok(await _dossierRepository.GetDossierByInputOfficerListAsync(filter, scope.IsAdmin, scope.UnitId));
        }
    }
}
