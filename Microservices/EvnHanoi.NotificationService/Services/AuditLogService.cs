using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using EvnHanoi.NotificationService.Repositories;
using Serilog;

namespace EvnHanoi.NotificationService.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuditLogService(IAuditLogRepository auditLogRepository, IHttpClientFactory httpClientFactory)
        {
            _auditLogRepository = auditLogRepository;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<(long Total, IEnumerable<dynamic> Logs)> GetAuditLogsAsync(int page, int pageSize, string? keyword = null)
        {
            return await _auditLogRepository.GetAuditLogsAsync(page, pageSize, keyword);
        }

        public async Task<IEnumerable<dynamic>> GetRecentAuditLogsAsync(int count)
        {
            return await _auditLogRepository.GetRecentAuditLogsAsync(count);
        }

        public async Task<long> DeleteAuditLogsAsync(DateTime fromDate, DateTime toDate, string? username, string? userId)
        {
            return await _auditLogRepository.DeleteAuditLogsAsync(fromDate, toDate, username, userId);
        }

        public async Task<bool> CheckPermissionAsync(string? authHeader, ClaimsPrincipal user, string permissionCode)
        {
            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            var username = user.FindFirst(ClaimTypes.Name)?.Value ?? user.Identity?.Name;
            
            if (roles.Any(r => string.Equals(r, "ADMIN", StringComparison.OrdinalIgnoreCase)) || 
                string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrEmpty(authHeader))
            {
                return false;
            }

            var client = _httpClientFactory.CreateClient("IdentityService");
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/permissions");
            requestMessage.Headers.Add("Authorization", authHeader);

            try
            {
                var response = await client.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                var permissions = await response.Content.ReadFromJsonAsync<List<string>>();
                return permissions != null && permissions.Any(p => string.Equals(p, permissionCode, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking permission {PermissionCode} with IdentityService", permissionCode);
                return false;
            }
        }
    }
}
