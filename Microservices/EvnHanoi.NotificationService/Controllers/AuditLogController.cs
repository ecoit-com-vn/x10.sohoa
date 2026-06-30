using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using EvnHanoi.NotificationService.Services;

namespace EvnHanoi.NotificationService.Controllers
{
    [ApiController]
    [BypassDynamicPermission]
    [Route("api/v1/audit-logs")]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        [HttpGet("recent")]
        [Authorize]
        public async Task<IActionResult> GetRecentAuditLogs()
        {
            var authHeader = Request.Headers["Authorization"].ToString();

            if (!await _auditLogService.CheckPermissionAsync(authHeader, User, "VIEW_DASHBOARD"))
            {
                return StatusCode(403, new { message = "Không có quyền truy cập Dashboard." });
            }

            try
            {
                var logs = await _auditLogService.GetRecentAuditLogsAsync(5);
                return Ok(new
                {
                    Logs = logs
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (!await _auditLogService.CheckPermissionAsync(authHeader, User, "AUDIT_LOG_VIEW"))
            {
                return StatusCode(403, new { message = "Không có quyền xem nhật ký hệ thống." });
            }

            try
            {
                var (total, logs) = await _auditLogService.GetAuditLogsAsync(page, pageSize, keyword);
                return Ok(new
                {
                    items = logs,
                    totalCount = total,
                    page,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> DeleteAuditLogs([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var isSuperAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "ADMIN");
            if (!isSuperAdmin)
            {
                return Forbid("Bạn không có quyền thực hiện chức năng này. Chỉ SuperAdmin mới được quyền dọn dẹp nhật ký.");
            }

            var authHeader = Request.Headers["Authorization"].ToString();
            if (!await _auditLogService.CheckPermissionAsync(authHeader, User, "AUDIT_LOG_DELETE"))
            {
                return StatusCode(403, new { message = "Không có quyền dọn dẹp nhật ký hệ thống." });
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            try
            {
                var count = await _auditLogService.DeleteAuditLogsAsync(fromDate, toDate, username, userId);
                return Ok(new { message = $"Đã thực hiện dọn dẹp ẩn danh {count} bản ghi nhật ký hệ thống thành công.", Count = count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
