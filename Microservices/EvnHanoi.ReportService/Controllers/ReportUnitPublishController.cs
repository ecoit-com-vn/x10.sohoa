// Microservices/EvnHanoi.ReportService/Controllers/ReportUnitPublishController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using EvnHanoi.Infrastructure.Security;
using EvnHanoi.ReportService.Core.DTOs;
using EvnHanoi.ReportService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.ReportService.Controllers
{
    /// <summary>
    /// API cấu hình Nhóm báo cáo đơn vị (Report Unit Publish).
    /// Định tuyến qua route sẵn có "/api/v1/reports/**" của YARP Gateway.
    /// GET reports -> REPORT_UNIT_PUBLISH_VIEW; save -> REPORT_UNIT_PUBLISH_EDIT; publish -> REPORT_UNIT_PUBLISH_RELEASE.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/reports/unit-publish")]
    public class ReportUnitPublishController : ControllerBase
    {
        private readonly IReportRepository _reportRepository;

        public ReportUnitPublishController(IReportRepository reportRepository)
        {
            _reportRepository = reportRepository;
        }

        private bool IsAdmin => User.IsInRole("ADMIN") ||
                                 User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");

        private string Username => User.FindFirst("preferred_username")?.Value
                                    ?? User.FindFirst(ClaimTypes.Name)?.Value
                                    ?? "SYSTEM";

        private long? GetClaimUnitId()
        {
            var unitIdClaim = User.FindFirst("unit_id")?.Value;
            return long.TryParse(unitIdClaim, out var unitId) && unitId > 0 ? unitId : null;
        }

        /// <summary>Danh sách báo cáo cấu hình cho đơn vị. Admin hệ thống truyền unitId, còn lại lấy từ claim unit_id.</summary>
        [HttpGet("reports")]
        public async Task<IActionResult> GetReports([FromQuery] long? unitId)
        {
            long? resolvedUnitId = IsAdmin ? unitId ?? GetClaimUnitId() : GetClaimUnitId();

            if (resolvedUnitId is null)
                return BadRequest(new { message = "Không xác định được đơn vị để tải cấu hình báo cáo." });

            var items = await _reportRepository.GetReportUnitPublishesAsync(resolvedUnitId.Value);
            var dtos = items.Select(i => new ReportUnitPublishDto
            {
                Id = i.Id > 0 ? i.Id : null,
                ReportId = i.ReportId,
                ReportCode = i.ReportCode,
                ReportName = i.ReportName,
                IsPublish = i.IsPublish == 1,
                RoleIds = i.RoleIds
            });

            return Ok(dtos);
        }

        /// <summary>Lưu nháp cấu hình vai trò xem báo cáo (chưa áp dụng).</summary>
        [HttpPost("save")]
        public Task<IActionResult> Save([FromBody] ReportUnitPublishSaveDto dto) => SaveInternal(dto, isPublish: false);

        /// <summary>Công bố cấu hình vai trò xem báo cáo (áp dụng ngay).</summary>
        [HttpPost("publish")]
        public Task<IActionResult> Publish([FromBody] ReportUnitPublishSaveDto dto) => SaveInternal(dto, isPublish: true);

        private async Task<IActionResult> SaveInternal(ReportUnitPublishSaveDto dto, bool isPublish)
        {
            if (dto == null || dto.ReportId <= 0)
                return BadRequest(new { message = "Thông tin báo cáo không hợp lệ." });

            long? resolvedUnitId = IsAdmin ? dto.UnitId ?? GetClaimUnitId() : GetClaimUnitId();

            if (resolvedUnitId is null)
                return BadRequest(new { message = "Không xác định được đơn vị để lưu cấu hình báo cáo." });

            var success = await _reportRepository.SaveReportUnitPublishAsync(
                resolvedUnitId.Value,
                dto.ReportId,
                isPublish ? 1 : 0,
                dto.RoleIds ?? new List<long>(),
                Username);

            return success
                ? Ok(new { success = true, isPublish })
                : BadRequest(new { message = "Lưu cấu hình nhóm báo cáo đơn vị thất bại." });
        }

        /// <summary>API cho user thường: danh sách báo cáo đã công bố mà user được phép xem theo vai trò của họ trong đơn vị.</summary>
        [HttpGet("my-reports")]
        [BypassDynamicPermission]
        public async Task<IActionResult> GetMyReports()
        {
            var unitId = GetClaimUnitId();
            if (unitId is null)
                return Ok(Array.Empty<object>());

            var roleCodes = GetUserRoleCodes(unitId.Value);
            if (roleCodes.Count == 0)
                return Ok(Array.Empty<object>());

            var reports = await _reportRepository.GetPublishedReportsForUserAsync(unitId.Value, roleCodes);
            return Ok(reports.Select(r => new { r.Id, r.Code, r.Name }));
        }

        private List<string> GetUserRoleCodes(long unitId)
        {
            var roleCodes = User.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role" || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

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
                    // Ignore format error if unit_roles claim differs
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
