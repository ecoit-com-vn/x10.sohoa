using System.Net.Http.Json;
using EvnHanoi.NotificationService.Models;
using Microsoft.Extensions.Caching.Memory;

namespace EvnHanoi.NotificationService.Services;

public interface IDossierMenuScopeValidator
{
    Task<(bool Allowed, string? ErrorMessage)> ValidateAsync(
        string? menuScope,
        string? tab,
        string? userId,
        string? authorizationHeader,
        bool isAdmin);
}

/// <summary>
/// Kiểm tra menuScope + tab hợp lệ và quyền JWT (gọi IdentityService).
/// NotificationService không dùng DynamicPermissionFilter trên SearchController.
/// </summary>
public class DossierMenuScopeValidator : IDossierMenuScopeValidator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public DossierMenuScopeValidator(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public async Task<(bool Allowed, string? ErrorMessage)> ValidateAsync(
        string? menuScope,
        string? tab,
        string? userId,
        string? authorizationHeader,
        bool isAdmin)
    {
        var scope = DossierMenuScopes.Normalize(menuScope);
        if (scope is null)
        {
            // Tra cứu / caller cũ không truyền menuScope — dùng visibility legacy theo tab.
            if (string.IsNullOrWhiteSpace(userId))
                return (false, "Không xác định được người dùng từ token.");

            if (isAdmin)
                return (true, null);

            var legacyPerms = await GetUserPermissionsAsync(userId, authorizationHeader);
            if (HasAnyPermission(legacyPerms, "DOSSIER_VIEW", "DOSSIER_CREATE", "DOSSIER_MANAGE", "SUPER_ADMIN"))
                return (true, null);

            return (false, "Không có quyền tra cứu hồ sơ.");
        }

        if (string.IsNullOrWhiteSpace(userId))
            return (false, "Không xác định được người dùng từ token.");

        var tabSlug = tab?.Trim().ToLowerInvariant();

        if (DossierMenuScopes.IsCreator(scope))
        {
            if (string.Equals(tabSlug, DossierListTabs.PendingAction, StringComparison.Ordinal))
                return (false, "Tab pending-action không áp dụng cho menu quản lý hồ sơ.");

            if (isAdmin)
                return (true, null);

            var perms = await GetUserPermissionsAsync(userId, authorizationHeader);
            if (HasAnyPermission(perms, "DOSSIER_CREATE", "DOSSIER_VIEW", "SUPER_ADMIN"))
                return (true, null);

            return (false, "Không có quyền truy cập menu quản lý hồ sơ.");
        }

        if (DossierMenuScopes.IsPublisher(scope))
        {
            if (tabSlug is not null && 
                tabSlug != DossierListTabs.PendingPublish && 
                tabSlug != DossierListTabs.Published && 
                tabSlug != DossierListTabs.Unpublished)
            {
                return (false, "Tab này không áp dụng cho menu xuất bản hồ sơ.");
            }

            if (isAdmin)
                return (true, null);

            var perms = await GetUserPermissionsAsync(userId, authorizationHeader);
            if (HasAnyPermission(perms, "DOSSIER_PUBLISH_VIEW", "DOSSIER_PUBLISH_RELEASE", "SUPER_ADMIN"))
                return (true, null);

            return (false, "Không có quyền truy cập menu xuất bản hồ sơ.");
        }

        if (DossierMenuScopes.IsEquipmentLookup(scope))
        {
            if (isAdmin)
                return (true, null);

            var lookupPerms = await GetUserPermissionsAsync(userId, authorizationHeader);
            if (HasAnyPermission(lookupPerms, "SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW", "SUPER_ADMIN"))
                return (true, null);

            return (false, "Không có quyền tra cứu hồ sơ thiết bị.");
        }

        // approver
        if (string.Equals(tabSlug, DossierListTabs.Draft, StringComparison.Ordinal) ||
            string.Equals(tabSlug, DossierListTabs.Returned, StringComparison.Ordinal))
        {
            return (false, "Tab này không áp dụng cho menu phê duyệt hồ sơ.");
        }

        if (isAdmin)
            return (true, null);

        var approverPerms = await GetUserPermissionsAsync(userId, authorizationHeader);
        if (HasAllPermissions(approverPerms, "DOSSIER_MANAGE", "DOSSIER_VIEW", "DOSSIER_EDIT"))
            return (true, null);

        return (false, "Không có quyền truy cập menu phê duyệt hồ sơ.");
    }

    private async Task<List<string>> GetUserPermissionsAsync(string userId, string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return new List<string>();

        var cacheKey = $"UserPermsCodes_{userId}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached is not null)
            return cached;

        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/permissions");
            request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new List<string>();

            var perms = await response.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
            _cache.Set(cacheKey, perms, TimeSpan.FromMinutes(5));
            return perms;
        }
        catch
        {
            return new List<string>();
        }
    }

    private static bool HasAnyPermission(IReadOnlyList<string> perms, params string[] codes) =>
        codes.Any(code => perms.Any(p => string.Equals(p, code, StringComparison.OrdinalIgnoreCase)));

    private static bool HasAllPermissions(IReadOnlyList<string> perms, params string[] codes) =>
        codes.All(code => perms.Any(p => string.Equals(p, code, StringComparison.OrdinalIgnoreCase)));
}
