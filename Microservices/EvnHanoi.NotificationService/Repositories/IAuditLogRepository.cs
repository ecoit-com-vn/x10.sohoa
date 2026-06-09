using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.NotificationService.Repositories
{
    public interface IAuditLogRepository
    {
        Task<(long Total, IEnumerable<dynamic> Logs)> GetAuditLogsAsync(int page, int pageSize);
        Task<IEnumerable<dynamic>> GetRecentAuditLogsAsync(int count);
        Task<long> DeleteAuditLogsAsync(DateTime fromDate, DateTime toDate, string? username, string? userId);
    }
}
