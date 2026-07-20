// Microservices/EvnHanoi.ReportService/Controllers/ReportStatisticsController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.ReportService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.ReportService.Controllers
{
    /// <summary>
    /// API Thống kê báo cáo (Report Statistics).
    /// Định tuyến qua route "/api/v1/reports/statistics" của YARP Gateway.
    /// Yêu cầu quyền: REPORT_STATISTICS_VIEW.
    /// Dạng partial class để dễ dàng mở rộng các API báo cáo chi tiết trong tương lai.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/reports/statistics")]
    public partial class ReportStatisticsController : ControllerBase
    {
        private readonly IReportRepository _reportRepository;
        private readonly IReportDossierRepository _dossierRepository;

        public ReportStatisticsController(
            IReportRepository reportRepository,
            IReportDossierRepository dossierRepository)
        {
            _reportRepository = reportRepository;
            _dossierRepository = dossierRepository;
        }

        protected Core.Models.UserScope ResolveUserScope()
        {
            var isAdmin = User.IsInRole("ADMIN") ||
                          User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");

            long? unitId = null;
            if (!isAdmin)
            {
                var unitIdClaim = User.FindFirst("unit_id")?.Value;
                if (long.TryParse(unitIdClaim, out var userUnitId) && userUnitId > 0)
                    unitId = userUnitId;
            }

            return new Core.Models.UserScope { IsAdmin = isAdmin, UnitId = unitId };
        }

        /// <summary>
        /// Lookup danh sách đơn vị
        /// GET /api/v1/reports/statistics/lookups/units
        /// </summary>
        [HttpGet("lookups/units")]
        public async Task<IActionResult> GetUnitsLookup()
        {
            var scope = ResolveUserScope();
            var units = await _dossierRepository.GetOrganizationUnitsAsync(scope.IsAdmin, scope.UnitId);
            return Ok(units);
        }

        /// <summary>
        /// Lookup danh sách loại đối tượng
        /// GET /api/v1/reports/statistics/lookups/object-types
        /// </summary>
        [HttpGet("lookups/object-types")]
        public IActionResult GetObjectTypesLookup()
        {
            var types = new[]
            {
                new { Id = 0, Code = "ALL", Name = "Tất cả" },
                new { Id = 1, Code = "STATION", Name = "Trạm biến áp" },
                new { Id = 2, Code = "LINE", Name = "Đường dây" },
                new { Id = 3, Code = "EQUIPMENT", Name = "Thiết bị" }
            };
            return Ok(types);
        }

        /// <summary>
        /// Lookup danh sách năm báo cáo khả dụng
        /// GET /api/v1/reports/statistics/lookups/years
        /// </summary>
        [HttpGet("lookups/years")]
        public async Task<IActionResult> GetYearsLookup()
        {
            var years = await _dossierRepository.GetAvailableYearsAsync();
            return Ok(years);
        }

        /// <summary>
        /// Lookup danh sách tháng báo cáo khả dụng (nhóm theo năm — FE render combobox grouped)
        /// GET /api/v1/reports/statistics/lookups/months
        /// </summary>
        [HttpGet("lookups/months")]
        public async Task<IActionResult> GetMonthsLookup()
        {
            var months = await _dossierRepository.GetAvailableMonthsAsync();
            return Ok(months);
        }

        /// <summary>
        /// Lookup cột danh mục BHS (catalogType.Code = BHS) — dùng chung tab Danh sách hồ sơ các báo cáo thống kê.
        /// GET /api/v1/reports/statistics/lookups/bhs-columns
        /// </summary>
        [HttpGet("lookups/bhs-columns")]
        public async Task<IActionResult> GetBhsColumnsLookup()
        {
            var columns = await _dossierRepository.GetBhsColumnsAsync();
            return Ok(columns);
        }

        /// <summary>
        /// Lookup danh sách loại lưới điện (GridTypes) — dùng cho báo cáo thống kê theo lưới điện áp.
        /// GET /api/v1/reports/statistics/lookups/grid-types
        /// </summary>
        [HttpGet("lookups/grid-types")]
        public async Task<IActionResult> GetGridTypesLookup()
        {
            var scope = ResolveUserScope();
            var effectiveUnitId = scope.IsAdmin ? null : scope.UnitId;
            var gridTypes = await _dossierRepository.GetGridTypesAsync(effectiveUnitId);
            return Ok(gridTypes);
        }

        /// <summary>
        /// Lookup danh sách loại thiết bị — dùng cho báo cáo thống kê theo loại thiết bị.
        /// GET /api/v1/reports/statistics/lookups/equipment-types
        /// </summary>
        [HttpGet("lookups/equipment-types")]
        public async Task<IActionResult> GetEquipmentTypesLookup()
        {
            var scope = ResolveUserScope();
            var effectiveUnitId = scope.IsAdmin ? null : scope.UnitId;
            var equipmentTypes = await _dossierRepository.GetEquipmentTypesAsync(effectiveUnitId);
            return Ok(equipmentTypes);
        }

        private long? GetClaimUnitId()
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            return long.TryParse(unitIdClaim, out var unitId) && unitId > 0 ? unitId : null;
        }

        /// <summary>
        /// Danh sách các báo cáo đã công bố mà người dùng hiện tại được phép xem theo vai trò trong đơn vị.
        /// GET /api/v1/reports/statistics/my-reports
        /// </summary>
        [HttpGet("my-reports")]
        public async Task<IActionResult> GetMyReports()
        {
            var unitId = GetClaimUnitId();
            if (unitId is null)
                return Ok(Array.Empty<object>());

            var roleCodes = GetUserRoleCodes(unitId.Value);
            if (roleCodes.Count == 0)
                return Ok(Array.Empty<object>());

            var reports = await _reportRepository.GetPublishedReportsForUserAsync(unitId.Value, roleCodes);
            var dtos = reports.Select(r => new
            {
                r.Id,
                r.Code,
                r.Name
            });

            return Ok(dtos);
        }

        /// <summary>
        /// Alias endpoint: GET /api/v1/reports/statistics
        /// </summary>
        [HttpGet]
        public Task<IActionResult> GetReports() => GetMyReports();

        /// <summary>
        /// Lấy danh sách mã vai trò của người dùng từ các claim tiêu chuẩn (ClaimTypes.Role) và unit_roles (nếu có).
        /// </summary>
        private List<string> GetUserRoleCodes(long unitId)
        {
            // 1. Lấy tất cả role từ claim tiêu chuẩn của JWT (ClaimTypes.Role hoặc claim "role")
            var roleCodes = User.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role" || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            // 2. Nếu có claim unit_roles bổ sung, parse để lấy các vai trò thuộc unitId này
            var unitRolesClaim = User.FindFirst("unit_roles")?.Value;
            if (!string.IsNullOrEmpty(unitRolesClaim))
            {
                try
                {
                    var unitRoles = JsonSerializer.Deserialize<List<UnitRoleClaimDto>>(unitRolesClaim, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (unitRoles != null)
                    {
                        var matchedRoles = unitRoles
                            .Where(ur => ur.UnitId == unitId && !string.IsNullOrEmpty(ur.RoleCode))
                            .Select(ur => ur.RoleCode);
                        roleCodes.AddRange(matchedRoles);
                    }
                }
                catch
                {
                    // Bỏ qua nếu claim unit_roles không đúng định dạng JSON
                }
            }

            return roleCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private class UnitRoleClaimDto
        {
            public long UnitId { get; set; }
            public string RoleCode { get; set; } = string.Empty;
        }
    }
}
