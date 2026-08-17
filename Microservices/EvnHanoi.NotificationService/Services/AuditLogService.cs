using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using EvnHanoi.Infrastructure.Audit;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Repositories;
using Serilog;

namespace EvnHanoi.NotificationService.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IAuditLogExportService _auditLogExportService;
        private readonly IAuditLogRetentionSettingsClient _retentionSettingsClient;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuditLogService(
            IAuditLogRepository auditLogRepository,
            IAuditLogExportService auditLogExportService,
            IAuditLogRetentionSettingsClient retentionSettingsClient,
            IHttpClientFactory httpClientFactory)
        {
            _auditLogRepository = auditLogRepository;
            _auditLogExportService = auditLogExportService;
            _retentionSettingsClient = retentionSettingsClient;
            _httpClientFactory = httpClientFactory;
        }

        public Task<(long Total, IReadOnlyList<AuditLogItemDto> Logs)> GetAuditLogsAsync(
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
            IReadOnlyList<string>? unitIds = null)
        {
            return _auditLogRepository.GetAuditLogsAsync(
                page, pageSize, keyword, action, resourceType, serviceName, userName, fromDate, toDate, logGroup, unitIds);
        }

        public Task<IReadOnlyList<AuditLogItemDto>> GetRecentAuditLogsAsync(int count)
        {
            return _auditLogRepository.GetRecentAuditLogsAsync(count);
        }

        public Task<long> GetDashboardDownloadCountAsync(DateTime? fromDate = null, DateTime? toDate = null, string? unitId = null)
        {
            return _auditLogRepository.GetDashboardDownloadCountAsync(fromDate, toDate, unitId);
        }

        public async Task<(byte[] FileBytes, string FileName, int RowCount)> ExportAuditLogsAsync(
            DateTime fromDate,
            DateTime toDate,
            string? keyword = null,
            string? action = null,
            string? resourceType = null,
            string? serviceName = null,
            string? userName = null,
            string? logGroup = null,
            IReadOnlyList<string>? unitIds = null)
        {
            var logs = await _auditLogRepository.ExportAuditLogsAsync(
                keyword, action, resourceType, serviceName, userName, fromDate, toDate, logGroup: logGroup, unitIds: unitIds);

            var bytes = _auditLogExportService.BuildExcel(logs);
            var fileName = $"AuditLog_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
            return (bytes, fileName, logs.Count);
        }

        public AuditLogLookupsDto GetLookups(string? logGroup)
        {
            var resourceTypeQuery = AuditVietnameseLabels.ResourceTypeLabels.AsEnumerable();
            if (string.Equals(logGroup, AuditLogGroups.Business, StringComparison.OrdinalIgnoreCase))
            {
                resourceTypeQuery = resourceTypeQuery.Where(kv => kv.Key.StartsWith("DOSSIER", StringComparison.OrdinalIgnoreCase));
            }
            else if (string.Equals(logGroup, AuditLogGroups.Operation, StringComparison.OrdinalIgnoreCase))
            {
                resourceTypeQuery = resourceTypeQuery.Where(kv => !kv.Key.StartsWith("DOSSIER", StringComparison.OrdinalIgnoreCase));
            }

            return new AuditLogLookupsDto
            {
                Actions = AuditVietnameseLabels.ActionLabels
                    .Select(kv => new AuditLogLookupItem { Code = kv.Key, Label = kv.Value })
                    .ToList(),
                ResourceTypes = resourceTypeQuery
                    .Select(kv => new AuditLogLookupItem { Code = kv.Key, Label = kv.Value })
                    .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                    .ToList(),
                LogGroups = new List<AuditLogLookupItem>
                {
                    new() { Code = AuditLogGroups.Operation, Label = "Nhật ký thao tác" },
                    new() { Code = AuditLogGroups.Business, Label = "Nhật ký nghiệp vụ" }
                }
            };
        }

        public Task<long> DeleteAuditLogsAsync(DateTime fromDate, DateTime toDate, string? username, string? userId)
        {
            return _auditLogRepository.DeleteAuditLogsAsync(fromDate, toDate, username, userId);
        }

        public Task<long> DeleteAuditLogsByIdsAsync(IReadOnlyList<string> ids, string? username, string? userId)
        {
            return _auditLogRepository.DeleteAuditLogsByIdsAsync(ids, username, userId);
        }

        public async Task<AuditLogRetentionStatusDto> GetRetentionStatusAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var retentionDays = await _retentionSettingsClient.GetRetentionDaysAsync(cancellationToken);
            if (!retentionDays.HasValue)
                throw new InvalidOperationException("Không thể đọc thời gian lưu nhật ký hệ thống.");

            var nowUtc = DateTime.UtcNow;
            var nextCleanupAtUtc = GetNextCleanupAtUtc(nowUtc);
            var items = await _auditLogRepository.GetAuditLogIndexMetadataAsync(cancellationToken);
            var totalDocuments = items.Sum(item => item.DocumentCount);
            var totalSizeBytes = items.Sum(item => item.SizeBytes);
            var offset = (long)(pageNumber - 1) * pageSize;

            var pageItems = items
                .Skip(offset > int.MaxValue ? int.MaxValue : (int)offset)
                .Take(pageSize)
                .Select(item =>
                {
                    var estimatedDeleteAtUtc = item.LogDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                        .AddDays(retentionDays.Value)
                        .AddHours(1);
                    var remainingDays = Math.Max(0, (int)Math.Ceiling((estimatedDeleteAtUtc - nowUtc).TotalDays));

                    return new AuditLogRetentionIndexDto
                    {
                        IndexName = item.IndexName,
                        LogDate = item.LogDate,
                        DocumentCount = item.DocumentCount,
                        SizeBytes = item.SizeBytes,
                        EstimatedDeleteAtUtc = estimatedDeleteAtUtc,
                        RemainingDays = remainingDays,
                        Status = remainingDays == 0 ? "EXPIRING_SOON" : "ACTIVE"
                    };
                })
                .ToList();

            return new AuditLogRetentionStatusDto
            {
                RetentionDays = retentionDays.Value,
                NextCleanupAtUtc = nextCleanupAtUtc,
                TotalIndices = items.Count,
                TotalDocuments = totalDocuments,
                TotalSizeBytes = totalSizeBytes,
                Items = pageItems,
                TotalCount = items.Count,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public Task<(IReadOnlyList<string> DeletedIndices, long DeletedDocuments)> PurgeExpiredAuditLogsAsync(
            DateTime cutoffUtc,
            CancellationToken cancellationToken = default)
        {
            return _auditLogRepository.PurgeExpiredAuditLogsAsync(cutoffUtc, cancellationToken);
        }

        public Task<long> DeleteAuditLogIndexAsync(
            DateOnly logDate,
            CancellationToken cancellationToken = default)
        {
            return _auditLogRepository.DeleteAuditLogIndexAsync(logDate, cancellationToken);
        }

        public Task<(int DeletedIndices, long DeletedDocuments)> DeleteAllAuditLogIndicesAsync(
            DateOnly excludedDate,
            CancellationToken cancellationToken = default)
        {
            return _auditLogRepository.DeleteAllAuditLogIndicesAsync(excludedDate, cancellationToken);
        }

        private static DateTime GetNextCleanupAtUtc(DateTime nowUtc)
        {
            var nextCleanupAtUtc = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 1, 0, 0, DateTimeKind.Utc);
            return nextCleanupAtUtc > nowUtc ? nextCleanupAtUtc : nextCleanupAtUtc.AddDays(1);
        }

        public async Task<bool> CheckPermissionAsync(string? authHeader, ClaimsPrincipal user, string permissionCode)
        {
            return await CheckAnyPermissionAsync(authHeader, user, permissionCode);
        }

        public async Task<bool> CheckAnyPermissionAsync(
            string? authHeader,
            ClaimsPrincipal user,
            params string[] permissionCodes)
        {
            if (permissionCodes.Length == 0)
                return false;

            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            var username = user.FindFirst(ClaimTypes.Name)?.Value ?? user.Identity?.Name;

            if (roles.Any(r => string.Equals(r, "ADMIN", StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrEmpty(authHeader))
                return false;

            var client = _httpClientFactory.CreateClient("IdentityService");
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/permissions");
            requestMessage.Headers.Add("Authorization", authHeader);

            try
            {
                var response = await client.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode)
                    return false;

                var permissions = await response.Content.ReadFromJsonAsync<List<string>>();
                return permissions != null && permissionCodes.Any(code =>
                    permissions.Any(p => string.Equals(p, code, StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking permissions [{Permissions}] with IdentityService",
                    string.Join(", ", permissionCodes));
                return false;
            }
        }
    }
}
