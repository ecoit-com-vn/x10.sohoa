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
            DateTime? toDate = null,
            string? logGroup = null,
            IReadOnlyList<string>? unitIds = null);

        Task<IReadOnlyList<AuditLogItemDto>> GetRecentAuditLogsAsync(int count);
        Task<long> GetDashboardDownloadCountAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<IReadOnlyList<AuditLogItemDto>> ExportAuditLogsAsync(
            string? keyword = null,
            string? action = null,
            string? resourceType = null,
            string? serviceName = null,
            string? userName = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int maxRows = 50000,
            string? logGroup = null,
            IReadOnlyList<string>? unitIds = null);
        Task<long> DeleteAuditLogsAsync(DateTime fromDate, DateTime toDate, string? username, string? userId);
        Task<long> DeleteAuditLogsByIdsAsync(IReadOnlyList<string> ids, string? username, string? userId);
        Task<IReadOnlyList<AuditLogIndexMetadata>> GetAuditLogIndexMetadataAsync(
            CancellationToken cancellationToken = default);
        Task<long> DeleteAuditLogIndexAsync(
            DateOnly logDate,
            CancellationToken cancellationToken = default);
        Task<(int DeletedIndices, long DeletedDocuments)> DeleteAllAuditLogIndicesAsync(
            DateOnly excludedDate,
            CancellationToken cancellationToken = default);
        Task<(IReadOnlyList<string> DeletedIndices, long DeletedDocuments)> PurgeExpiredAuditLogsAsync(
            DateTime cutoffUtc,
            CancellationToken cancellationToken = default);
    }
}
