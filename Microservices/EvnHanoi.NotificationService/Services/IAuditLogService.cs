using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EvnHanoi.NotificationService.Services
{
    public interface IAuditLogService
    {
        Task<(long Total, IEnumerable<dynamic> Logs)> GetAuditLogsAsync(int page, int pageSize);
        Task<IEnumerable<dynamic>> GetRecentAuditLogsAsync(int count);
        Task<long> DeleteAuditLogsAsync(DateTime fromDate, DateTime toDate, string? username, string? userId);
        Task<bool> CheckPermissionAsync(string? authHeader, ClaimsPrincipal user, string permissionCode);
    }
}
