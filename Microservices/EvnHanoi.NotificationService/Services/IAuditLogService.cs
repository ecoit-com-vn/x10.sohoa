using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Services
{
    public interface IAuditLogService
    {
        Task<(long Total, IReadOnlyList<AuditLogItemDto> Logs)> GetAuditLogsAsync(
            int page,
            int pageSize,
            string? keyword = null,
            string? action = null,
            string? resourceType = null,
            string? serviceName = null,
            string? userName = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? logGroup = null,
            IReadOnlyList<string>? unitIds = null);

        Task<IReadOnlyList<AuditLogItemDto>> GetRecentAuditLogsAsync(int count);
        Task<(byte[] FileBytes, string FileName, int RowCount)> ExportAuditLogsAsync(
            DateTime fromDate,
            DateTime toDate,
            string? keyword = null,
            string? action = null,
            string? resourceType = null,
            string? serviceName = null,
            string? userName = null,
            string? logGroup = null,
            IReadOnlyList<string>? unitIds = null);
        Task<long> DeleteAuditLogsAsync(DateTime fromDate, DateTime toDate, string? username, string? userId);
        Task<long> DeleteAuditLogsByIdsAsync(IReadOnlyList<string> ids, string? username, string? userId);
        Task<bool> CheckPermissionAsync(string? authHeader, ClaimsPrincipal user, string permissionCode);
        Task<bool> CheckAnyPermissionAsync(string? authHeader, ClaimsPrincipal user, params string[] permissionCodes);
        AuditLogLookupsDto GetLookups(string? logGroup);
    }
}
