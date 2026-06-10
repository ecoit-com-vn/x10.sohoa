using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace EvnHanoi.Infrastructure.Security;

public class DynamicPermissionFilter : IAsyncActionFilter
{
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public DynamicPermissionFilter(IMemoryCache cache, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controllerName = context.RouteData.Values["controller"]?.ToString();
        var actionName = context.RouteData.Values["action"]?.ToString();
        var httpMethod = context.HttpContext.Request.Method;

        Log.Information("Shared DynamicPermissionFilter: Intercepting request for {Controller}/{Action} via {Method}", 
            controllerName, actionName, httpMethod);

        // Bypass checks if action is OPTIONS preflight request
        if (string.Equals(httpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        // Bypass checks if action is decorated with [AllowAnonymous]
        if (context.ActionDescriptor.EndpointMetadata.Any(em => em is AllowAnonymousAttribute))
        {
            await next();
            return;
        }

        // Bypass checks if action/controller is decorated with [BypassDynamicPermission]
        if (context.ActionDescriptor.EndpointMetadata.Any(em => em is BypassDynamicPermissionAttribute) ||
            context.ActionDescriptor.EndpointMetadata.Any(em => em.GetType().Name == "BypassDynamicPermissionAttribute"))
        {
            Log.Information("Shared DynamicPermissionFilter: Bypassing dynamic permission check due to BypassDynamicPermissionAttribute");
            await next();
            return;
        }

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            Log.Warning("Shared DynamicPermissionFilter: User is NOT authenticated");
            context.Result = new UnauthorizedResult();
            return;
        }

        if (string.IsNullOrEmpty(controllerName) || string.IsNullOrEmpty(actionName))
        {
            await next();
            return;
        }

        // Check if user has ADMIN role bypass
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var username = user.FindFirst(ClaimTypes.Name)?.Value ?? user.Identity?.Name;

        if (roles.Contains("ADMIN", StringComparer.OrdinalIgnoreCase) || 
            string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("Shared DynamicPermissionFilter: Bypassing check for ADMIN/admin user");
            await next();
            return;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            Log.Warning("Shared DynamicPermissionFilter: UserId claim is missing");
            context.Result = new ForbidResult();
            return;
        }

        // Standardize Controller Name
        if (!controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
        {
            controllerName += "Controller";
        }

        // Determine required permission code
        string resourceBase = GetResourceBase(controllerName.Replace("Controller", ""));
        string category = CategorizeAction(actionName, httpMethod);
        string requiredPermission = $"{resourceBase}_{category}";

        Log.Information("Shared DynamicPermissionFilter: Mapping request to permission code: '{RequiredPermission}'", requiredPermission);

        // Fetch allowed permissions from cache or call IdentityService
        var cacheKey = $"UserPermsCodes_{userId}";
        if (!_cache.TryGetValue(cacheKey, out List<string>? allowedPermissions))
        {
            var authHeader = context.HttpContext.Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader))
            {
                Log.Warning("Shared DynamicPermissionFilter: Authorization header is missing");
                context.Result = new ForbidResult();
                return;
            }

            Log.Information("Shared DynamicPermissionFilter: Fetching permissions from IdentityService for User: {UserId}", userId);
            try
            {
                var client = _httpClientFactory.CreateClient("IdentityService");
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/permissions");
                requestMessage.Headers.Add("Authorization", authHeader);

                var response = await client.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode)
                {
                    Log.Error("Shared DynamicPermissionFilter: Failed to fetch permissions from IdentityService. Status: {StatusCode}", response.StatusCode);
                    context.Result = new ObjectResult(new { message = "Không thể xác thực quyền truy cập với hệ thống." }) { StatusCode = 403 };
                    return;
                }

                allowedPermissions = await response.Content.ReadFromJsonAsync<List<string>>();
                
                // Cache user permissions for 5 minutes
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                _cache.Set(cacheKey, allowedPermissions ?? new List<string>(), cacheEntryOptions);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Shared DynamicPermissionFilter: Exception checking permission");
                context.Result = new ObjectResult(new { message = "Lỗi hệ thống khi kiểm tra quyền truy cập." }) { StatusCode = 500 };
                return;
            }
        }

        // Validate
        var isAuthorized = allowedPermissions != null && allowedPermissions.Any(p => string.Equals(p, requiredPermission, StringComparison.OrdinalIgnoreCase));
        if (!isAuthorized)
        {
            Log.Warning("Shared DynamicPermissionFilter: Access Denied. User does not have '{RequiredPermission}'", requiredPermission);
            context.Result = new ObjectResult(new { message = $"Không có quyền thực thi hành động '{actionName}' trên tài nguyên '{controllerName.Replace("Controller", "")}'." })
            {
                StatusCode = 403
            };
            return;
        }

        Log.Information("Shared DynamicPermissionFilter: Access Granted for {Controller}/{Action}", controllerName, actionName);
        await next();
    }

    private string GetResourceBase(string controllerKey)
    {
        return controllerKey switch
        {
            "Menus" => "MENU",
            "Users" => "USER",
            "Roles" => "ROLE",
            "Permissions" => "PERMISSION",
            "OrganizationUnits" => "ORGANIZATION",
            "UploadConfigs" => "UPLOAD_CONFIG",
            "SystemParams" => "SYSTEM_PARAM",
            "UserGroups" => "USER_GROUP",
            "AuditLog" => "AUDIT_LOG",
            "Signatures" => "SIGNATURE",
            "WorkflowDefinitions" => "WORKFLOW_DEFINITION",
            _ => ToSnakeCase(controllerKey)
        };
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (i > 0 && char.IsUpper(c))
            {
                if (input[i - 1] != '_' && (!char.IsUpper(input[i - 1]) || (i + 1 < input.Length && char.IsLower(input[i + 1]))))
                {
                    sb.Append('_');
                }
            }
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    private string CategorizeAction(string actionName, string httpMethod)
    {
        string actLower = actionName.ToLowerInvariant();

        // 0. MANAGE (Explicit management actions like assignment/grant/revoke)
        if (actLower.Contains("assign") || actLower.Contains("grant") || actLower.Contains("revoke"))
        {
            return "MANAGE";
        }

        if (actLower.Contains("import") || actLower.Contains("upload"))
        {
            return "IMPORT";
        }

        if (actLower.Contains("export") || actLower.Contains("download"))
        {
            return "EXPORT";
        }

        if (httpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase) ||
            actLower.StartsWith("delete") || actLower.StartsWith("remove") || actLower.StartsWith("destroy"))
        {
            return "DELETE";
        }

        if (httpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
            httpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase) ||
            actLower.StartsWith("update") || actLower.StartsWith("edit") || actLower.StartsWith("save") || actLower.StartsWith("patch"))
        {
            return "EDIT";
        }

        if (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            actLower.StartsWith("create") || actLower.StartsWith("add") || actLower.StartsWith("insert"))
        {
            return "CREATE";
        }

        if (httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) ||
            actLower.StartsWith("get") || actLower.StartsWith("find") || actLower.StartsWith("search") || actLower.StartsWith("load"))
        {
            return "VIEW";
        }

        return "MANAGE";
    }
}
