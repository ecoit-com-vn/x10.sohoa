using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using EvnHanoi.NotificationService.Models;
using EvnHanoi.NotificationService.Repositories;
using Serilog;

namespace EvnHanoi.NotificationService.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IAuditLogExportService _auditLogExportService;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuditLogService(
            IAuditLogRepository auditLogRepository,
            IAuditLogExportService auditLogExportService,
            IHttpClientFactory httpClientFactory)
        {
            _auditLogRepository = auditLogRepository;
            _auditLogExportService = auditLogExportService;
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
            DateTime? toDate = null)
        {
            return _auditLogRepository.GetAuditLogsAsync(
                page, pageSize, keyword, action, resourceType, serviceName, userName, fromDate, toDate);
        }

        public Task<IReadOnlyList<AuditLogItemDto>> GetRecentAuditLogsAsync(int count)
        {
            return _auditLogRepository.GetRecentAuditLogsAsync(count);
        }

        public async Task<(byte[] FileBytes, string FileName, int RowCount)> ExportAuditLogsAsync(
            DateTime fromDate,
            DateTime toDate,
            string? keyword = null,
            string? action = null,
            string? resourceType = null,
            string? serviceName = null,
            string? userName = null)
        {
            var logs = await _auditLogRepository.ExportAuditLogsAsync(
                keyword, action, resourceType, serviceName, userName, fromDate, toDate);

            var bytes = _auditLogExportService.BuildExcel(logs);
            var fileName = $"AuditLog_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
            return (bytes, fileName, logs.Count);
        }

        public Task<long> DeleteAuditLogsAsync(DateTime fromDate, DateTime toDate, string? username, string? userId)
        {
            return _auditLogRepository.DeleteAuditLogsAsync(fromDate, toDate, username, userId);
        }

        public Task<long> DeleteAuditLogsByIdsAsync(IReadOnlyList<string> ids, string? username, string? userId)
        {
            return _auditLogRepository.DeleteAuditLogsByIdsAsync(ids, username, userId);
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
