using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Interfaces;
using EvnHanoi.IdentityService.Core.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace EvnHanoi.IdentityService.Infrastructure.Security;

public class DynamicPermissionFilter : IAsyncActionFilter
{
    private readonly IMemoryCache _cache;

    public DynamicPermissionFilter(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controllerName = context.RouteData.Values["controller"]?.ToString();
        var actionName = context.RouteData.Values["action"]?.ToString();
        
        Log.Information("DynamicPermissionFilter: Intercepting request for {Controller}/{Action} via {Method}", 
            controllerName, actionName, context.HttpContext.Request.Method);

        // Bypass if it is an OPTIONS preflight request
        if (string.Equals(context.HttpContext.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("DynamicPermissionFilter: Bypassing OPTIONS preflight request");
            await next();
            return;
        }

        // Bypass checks if action is decorated with [AllowAnonymous]
        if (context.ActionDescriptor.EndpointMetadata.Any(em => em is AllowAnonymousAttribute))
        {
            Log.Information("DynamicPermissionFilter: Bypassing [AllowAnonymous] action");
            await next();
            return;
        }

        // Bypass checks if action is decorated with [BypassDynamicPermission]
        if (context.ActionDescriptor.EndpointMetadata.Any(em => em is BypassDynamicPermissionAttribute))
        {
            Log.Information("DynamicPermissionFilter: Bypassing dynamic permission check for standard system action");
            await next();
            return;
        }

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            Log.Warning("DynamicPermissionFilter: User is NOT authenticated");
            context.Result = new UnauthorizedResult();
            return;
        }

        if (string.IsNullOrEmpty(controllerName) || string.IsNullOrEmpty(actionName))
        {
            Log.Information("DynamicPermissionFilter: Bypassing due to missing controller or action name");
            await next();
            return;
        }

        // Standardize Controller Name (ensure suffix)
        if (!controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
        {
            controllerName += "Controller";
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Log.Information("DynamicPermissionFilter: Authenticated user ID = {UserId}", userId);
        
        if (string.IsNullOrEmpty(userId))
        {
            Log.Warning("DynamicPermissionFilter: UserId claim is missing");
            context.Result = new ForbidResult();
            return;
        }

        // Check if user has SUPER_ADMIN bypass (optional but highly standard)
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var username = user.FindFirst(ClaimTypes.Name)?.Value ?? user.Identity?.Name;
        
        Log.Information("DynamicPermissionFilter: Username = '{Username}', Roles = [{Roles}]", 
            username, string.Join(", ", roles));

        if (roles.Contains("ADMIN", StringComparer.OrdinalIgnoreCase) || 
            string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("DynamicPermissionFilter: Bypassing check for ADMIN/admin user");
            await next();
            return;
        }

        // Fetch user permissions from Cache or Database
        var cacheKey = $"UserPerms_{userId}";
        if (!_cache.TryGetValue(cacheKey, out IEnumerable<PermissionDetail>? allowedActions))
        {
            Log.Information("DynamicPermissionFilter: Cache miss for {CacheKey}. Fetching permissions from DB...", cacheKey);
            var permissionRepository = context.HttpContext.RequestServices.GetRequiredService<IPermissionRepository>();
            allowedActions = await permissionRepository.GetAllowedActionsForUserAsync(userId);
            
            // Cache for 5 minutes
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            _cache.Set(cacheKey, allowedActions, cacheEntryOptions);
            Log.Information("DynamicPermissionFilter: Fetched and cached {Count} permissions", allowedActions?.Count() ?? 0);
        }
        else
        {
            Log.Information("DynamicPermissionFilter: Cache hit for {CacheKey} with {Count} permissions", 
                cacheKey, allowedActions?.Count() ?? 0);
        }

        // Authorize if any PermissionDetail matches the Controller & Action (or wildcard "*")
        var isAuthorized = allowedActions != null && allowedActions.Any(pd =>
            pd.ControllerName.Equals(controllerName, StringComparison.OrdinalIgnoreCase) &&
            (pd.ActionName.Equals(actionName, StringComparison.OrdinalIgnoreCase) || pd.ActionName == "*"));

        if (!isAuthorized)
        {
            Log.Warning("DynamicPermissionFilter: Access Denied for {Controller}/{Action}", controllerName, actionName);
            context.Result = new ObjectResult(new { message = $"Không có quyền thực thi hành động '{actionName}' trên tài nguyên '{controllerName}'." })
            {
                StatusCode = 403
            };
            return;
        }

        Log.Information("DynamicPermissionFilter: Access Granted for {Controller}/{Action}", controllerName, actionName);
        await next();
    }
}
