using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Repositories
{
    public interface IAuditLogRepository
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
            DateTime? toDate = null);

        Task<IReadOnlyList<AuditLogItemDto>> GetRecentAuditLogsAsync(int count);
        Task<IReadOnlyList<AuditLogItemDto>> ExportAuditLogsAsync(
            string? keyword = null,
            string? action = null,
            string? resourceType = null,
            string? serviceName = null,
            string? userName = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int maxRows = 50000);
        Task<long> DeleteAuditLogsAsync(DateTime fromDate, DateTime toDate, string? username, string? userId);
        Task<long> DeleteAuditLogsByIdsAsync(IReadOnlyList<string> ids, string? username, string? userId);
    }
}
