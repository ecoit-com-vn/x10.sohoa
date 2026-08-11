using EvnHanoi.Infrastructure.Security;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RedLockNet;

namespace EvnHanoi.NotificationService.Controllers
{
    [ApiController]
    [BypassDynamicPermission]
    [Route("api/v1/audit-logs")]
    public class AuditLogController : ControllerBase
    {
        private const string AuditLogRetentionLockResource = "lock:audit-log-retention";
        private readonly IAuditLogService _auditLogService;
        private readonly IDistributedLockFactory _lockFactory;
        private readonly ILogger<AuditLogController> _logger;

        public AuditLogController(
            IAuditLogService auditLogService,
            IDistributedLockFactory lockFactory,
            ILogger<AuditLogController> logger)
        {
            _auditLogService = auditLogService;
            _lockFactory = lockFactory;
            _logger = logger;
        }

        [HttpGet("recent")]
        [Authorize]
        public async Task<IActionResult> GetRecentAuditLogs()
        {
            var authHeader = Request.Headers["Authorization"].ToString();

            if (!await _auditLogService.CheckPermissionAsync(authHeader, User, "VIEW_DASHBOARD"))
                return StatusCode(403, new { message = "Không có quyền truy cập Dashboard." });

            try
            {
                var logs = await _auditLogService.GetRecentAuditLogsAsync(5);
                return Ok(new { Logs = logs });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dashboard recent audit logs are unavailable.");
                return Ok(new { Logs = Array.Empty<AuditLogItemDto>() });
            }
        }

        [HttpGet("dashboard/download-count")]
        [Authorize]
        public async Task<IActionResult> GetDashboardDownloadCount(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (!await _auditLogService.CheckPermissionAsync(authHeader, User, "VIEW_DASHBOARD"))
                return StatusCode(403, new { message = "Không có quyền truy cập Dashboard." });

            try
            {
                var total = await _auditLogService.GetDashboardDownloadCountAsync(fromDate, toDate);
                return Ok(new { totalCount = total });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dashboard download count is unavailable.");
                return Ok(new { totalCount = 0L });
            }
        }

        [HttpGet("lookups")]
        [Authorize]
        public IActionResult GetLookups([FromQuery] string? logGroup = null)
        {
            return Ok(_auditLogService.GetLookups(logGroup));
        }

        [HttpGet("retention-status")]
        [Authorize]
        public async Task<IActionResult> GetRetentionStatus(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (!await _auditLogService.CheckAnyPermissionAsync(authHeader, User, "SYSTEM_PARAM_VIEW", "SYSTEM_PARAM_EDIT"))
                return StatusCode(403, new { message = "Không có quyền xem tình trạng lưu trữ nhật ký." });

            if (pageNumber < 1)
                return BadRequest(new { message = "pageNumber phải lớn hơn hoặc bằng 1." });

            if (pageSize < 1 || pageSize > 100)
                return BadRequest(new { message = "pageSize phải trong khoảng từ 1 đến 100." });

            try
            {
                return Ok(await _auditLogService.GetRetentionStatusAsync(pageNumber, pageSize, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Không thể tải tình trạng lưu trữ nhật ký." });
            }
        }

        [HttpDelete("retention-index/{logDate}")]
        [Authorize]
        public async Task<IActionResult> DeleteRetentionIndex(
            string logDate,
            CancellationToken cancellationToken = default)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (!await _auditLogService.CheckPermissionAsync(authHeader, User, "AUDIT_LOG_DELETE"))
                return StatusCode(403, new { message = "Không có quyền xóa index nhật ký." });

            if (!DateOnly.TryParseExact(
                    logDate,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var indexDate))
            {
                return BadRequest(new { message = "Ngày index không hợp lệ." });
            }

            if (indexDate == DateOnly.FromDateTime(DateTime.UtcNow))
                return BadRequest(new { message = "Không được xóa index nhật ký của ngày hiện tại." });

            var expiry = TimeSpan.FromHours(2);
            var wait = TimeSpan.FromSeconds(10);
            var retry = TimeSpan.FromSeconds(1);
            using var redLock = await _lockFactory.CreateLockAsync(AuditLogRetentionLockResource, expiry, wait, retry);
            if (!redLock.IsAcquired)
                return Conflict(new { message = "Tác vụ dọn dẹp nhật ký đang chạy. Vui lòng thử lại sau." });

            try
            {
                var deletedDocuments = await _auditLogService.DeleteAuditLogIndexAsync(indexDate, cancellationToken);
                return Ok(new
                {
                    message = "Đã xóa vật lý index nhật ký thành công.",
                    deletedDocuments
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Không tìm thấy index nhật ký cần xóa." });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Không thể xóa index nhật ký." });
            }
        }

        [HttpDelete("retention-indices")]
        [Authorize]
        public async Task<IActionResult> DeleteAllRetentionIndices(
            CancellationToken cancellationToken = default)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (!await _auditLogService.CheckPermissionAsync(authHeader, User, "AUDIT_LOG_DELETE"))
                return StatusCode(403, new { message = "Không có quyền xóa index nhật ký." });

            var expiry = TimeSpan.FromHours(2);
            var wait = TimeSpan.Zero;
            var retry = TimeSpan.Zero;
            using var redLock = await _lockFactory.CreateLockAsync(AuditLogRetentionLockResource, expiry, wait, retry);
            if (!redLock.IsAcquired)
                return Conflict(new { message = "Tác vụ dọn dẹp nhật ký đang chạy. Vui lòng thử lại sau." });

            try
            {
                var (deletedIndices, deletedDocuments) = await _auditLogService.DeleteAllAuditLogIndicesAsync(
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    cancellationToken);
                return Ok(new
                {
                    message = $"Đã xóa {deletedIndices} index nhật ký thành công.",
                    deletedIndices,
                    deletedDocuments
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Không thể xóa toàn bộ index nhật ký." });
            }
        }

        [HttpGet("export")]
        [Authorize]
        public async Task<IActionResult> ExportAuditLogs(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] string? keyword = null,
            [FromQuery] string? action = null,
            [FromQuery] string? resourceType = null,
            [FromQuery] string? serviceName = null,
            [FromQuery] string? userName = null,
            [FromQuery] string? logGroup = null,
            [FromQuery] List<string>? unitIds = null)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (!await _auditLogService.CheckAnyPermissionAsync(authHeader, User, "AUDIT_LOG_EXPORT", "AUDIT_LOG_VIEW"))
                return StatusCode(403, new { message = "Không có quyền xuất nhật ký hệ thống." });

            if (fromDate == default || toDate == default)
                return BadRequest(new { message = "Vui lòng chọn khoảng thời gian (fromDate, toDate)." });

            if (fromDate > toDate)
                return BadRequest(new { message = "Từ ngày không thể lớn hơn Đến ngày." });

            var maxRangeDays = 366;
            if ((toDate.Date - fromDate.Date).TotalDays > maxRangeDays)
                return BadRequest(new { message = $"Khoảng thời gian xuất file không được vượt quá {maxRangeDays} ngày." });

            try
            {
                var endOfDay = toDate.Date.AddDays(1).AddTicks(-1);
                var (fileBytes, fileName, rowCount) = await _auditLogService.ExportAuditLogsAsync(
                    fromDate.Date, endOfDay, keyword, action, resourceType, serviceName, userName, logGroup, unitIds);

                if (rowCount == 0)
                    return NotFound(new { message = "Không có bản ghi nhật ký trong khoảng thời gian đã chọn." });

                return File(
                    fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? keyword = null,
            [FromQuery] string? action = null,
            [FromQuery] string? resourceType = null,
            [FromQuery] string? serviceName = null,
            [FromQuery] string? userName = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? logGroup = null,
            [FromQuery] List<string>? unitIds = null)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (!await _auditLogService.CheckPermissionAsync(authHeader, User, "AUDIT_LOG_VIEW"))
                return StatusCode(403, new { message = "Không có quyền xem nhật ký hệ thống." });

            try
            {
                var (total, logs) = await _auditLogService.GetAuditLogsAsync(
                    page, pageSize, keyword, action, resourceType, serviceName, userName, fromDate, toDate, logGroup, unitIds);
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

        [HttpPost("bulk-delete")]
        [Authorize]
        public async Task<IActionResult> DeleteSelectedAuditLogs([FromBody] DeleteAuditLogsByIdsRequest request)
        {
            var isSuperAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c =>
                c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "ADMIN");
            if (!isSuperAdmin)
                return Forbid("Bạn không có quyền thực hiện chức năng này. Chỉ SuperAdmin mới được quyền dọn dẹp nhật ký.");

            var authHeader = Request.Headers["Authorization"].ToString();
            if (!await _auditLogService.CheckPermissionAsync(authHeader, User, "AUDIT_LOG_DELETE"))
                return StatusCode(403, new { message = "Không có quyền dọn dẹp nhật ký hệ thống." });

            if (request?.Ids is not { Count: > 0 })
                return BadRequest(new { message = "Vui lòng chọn ít nhất một nhật ký cần xóa." });

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            try
            {
                var count = await _auditLogService.DeleteAuditLogsByIdsAsync(request.Ids, username, userId);
                if (count == 0)
                    return NotFound(new { message = "Không tìm thấy nhật ký hợp lệ trong danh sách đã chọn." });

                return Ok(new
                {
                    message = $"Đã thực hiện dọn dẹp ẩn danh {count} nhật ký đã chọn thành công.",
                    Count = count
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
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
            var isSuperAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c =>
                c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "ADMIN");
            if (!isSuperAdmin)
                return Forbid("Bạn không có quyền thực hiện chức năng này. Chỉ SuperAdmin mới được quyền dọn dẹp nhật ký.");

            var authHeader = Request.Headers["Authorization"].ToString();
            if (!await _auditLogService.CheckPermissionAsync(authHeader, User, "AUDIT_LOG_DELETE"))
                return StatusCode(403, new { message = "Không có quyền dọn dẹp nhật ký hệ thống." });

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            try
            {
                var count = await _auditLogService.DeleteAuditLogsAsync(fromDate, toDate, username, userId);
                return Ok(new
                {
                    message = $"Đã thực hiện dọn dẹp ẩn danh {count} bản ghi nhật ký hệ thống thành công.",
                    Count = count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
