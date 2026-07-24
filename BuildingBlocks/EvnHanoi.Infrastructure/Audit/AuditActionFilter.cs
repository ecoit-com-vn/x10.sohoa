using System.Security.Claims;
using System.Text.Json;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EvnHanoi.Infrastructure.Audit;

/// <summary>
/// Tự động ghi audit cho mutation API (POST/PUT/PATCH/DELETE) và export/download.
/// </summary>
public sealed class AuditActionFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> MutationMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE"
    };

    private readonly IAuditPublisher _auditPublisher;
    private readonly AuditServiceMetadata _serviceMetadata;

    public AuditActionFilter(IAuditPublisher auditPublisher, AuditServiceMetadata serviceMetadata)
    {
        _auditPublisher = auditPublisher;
        _serviceMetadata = serviceMetadata;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        CaptureLoginUsername(context);
        CaptureRequestCode(context);
        var executedContext = await next();

        try
        {
            PublishIfNeeded(executedContext);
        }
        catch
        {
            // Audit không được làm fail request chính.
        }
    }

    private static void CaptureLoginUsername(ActionExecutingContext context)
    {
        var controllerName = context.RouteData.Values["controller"]?.ToString();
        var actionName = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
        if (!string.Equals(controllerName, "Auth", StringComparison.OrdinalIgnoreCase) ||
            !actionName.Contains("Login", StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var usernameProp = argument.GetType().GetProperty("Username")
                               ?? argument.GetType().GetProperty("UserName");
            if (usernameProp?.GetValue(argument) is string username && !string.IsNullOrWhiteSpace(username))
            {
                context.HttpContext.Items["__AuditLoginUsername"] = username.Trim();
                return;
            }
        }
    }

    private static void CaptureRequestCode(ActionExecutingContext context)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            var code = TryGetCodeFromFormDataJson(argument);
            if (!string.IsNullOrWhiteSpace(code))
            {
                context.HttpContext.Items["__AuditRequestCode"] = code;
                return;
            }
        }
    }

    private void PublishIfNeeded(ActionExecutedContext context)
    {
        var httpContext = context.HttpContext;
        var request = httpContext.Request;
        var method = request.Method;

        if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            return;

        if (IsSkipped(context))
            return;

        var path = request.Path.Value ?? string.Empty;
        if (path.Contains("/health", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/internal/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/hubs/", StringComparison.OrdinalIgnoreCase))
            return;

        var controllerName = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
        var actionName = context.RouteData.Values["action"]?.ToString() ?? "Unknown";
        var actionLower = actionName.ToLowerInvariant();
        var isExport = method.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                       (actionLower.Contains("export") || actionLower.Contains("download"));

        var isLogin = string.Equals(controllerName, "Auth", StringComparison.OrdinalIgnoreCase) &&
                      actionLower.Contains("login") &&
                      method.Equals("POST", StringComparison.OrdinalIgnoreCase);

        if (!MutationMethods.Contains(method) && !isExport && !isLogin)
            return;

        var isAnonymous = context.ActionDescriptor.EndpointMetadata.Any(em => em is AllowAnonymousAttribute);
        var user = httpContext.User;
        var isAuthenticated = user.Identity?.IsAuthenticated == true;

        if (isAnonymous && !isLogin)
            return;

        var statusCode = context.Result switch
        {
            ObjectResult objectResult => objectResult.StatusCode ?? httpContext.Response.StatusCode,
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            _ => httpContext.Response.StatusCode
        };

        if (statusCode == 0)
            statusCode = 200;

        var auditContext = AuditContext.Get(httpContext);
        var resourceType = auditContext?.ResourceType ?? PermissionCodeResolver.GetResourceBase(controllerName);
        var actionCategory = auditContext?.Action ?? PermissionCodeResolver.CategorizeAction(controllerName, actionName, method);
        var resourceId = auditContext?.ResourceId ?? ExtractResourceId(context);
        var resourceName = auditContext?.ResourceName;
        if (string.IsNullOrWhiteSpace(resourceName) && resourceType.StartsWith("DOSSIER", StringComparison.OrdinalIgnoreCase))
        {
            resourceName = ExtractCodeFromContext(context);
        }
        var details = auditContext?.Details ?? BuildDefaultDetails(actionCategory, resourceType, resourceName, resourceId);
        var logGroup = auditContext?.LogGroup ?? ResolveLogGroup(resourceType);

        string actorUserId;
        string actorUserName;

        if (isLogin)
        {
            actorUserName = ExtractLoginUsername(context) ?? "anonymous";
            actorUserId = isAuthenticated
                ? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? actorUserName
                : actorUserName;
            actionCategory = AuditActions.Login;
            details = statusCode is >= 200 and < 300
                ? $"Đăng nhập thành công: {actorUserName}"
                : $"Đăng nhập thất bại: {actorUserName}";
        }
        else
        {
            actorUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            actorUserName = user.FindFirst(ClaimTypes.Name)?.Value ?? user.Identity?.Name ?? "unknown";
        }

        if (AuditUserActionGuard.ShouldSkipHttpAudit(httpContext, isLogin, actorUserId, actorUserName))
            return;

        var actorUnitId = user.FindFirst("unit_id")?.Value;
        var actorFullName = user.FindFirst("full_name")?.Value;

        var auditEvent = new AuditEvent(
            Id: UuidHelper.NewUuid(),
            OccurredAt: DateTime.UtcNow,
            ServiceName: _serviceMetadata.ServiceName,
            ActorUserId: actorUserId,
            ActorUserName: actorUserName,
            ActorIp: httpContext.Connection.RemoteIpAddress?.ToString(),
            Action: actionCategory,
            ResourceType: resourceType,
            ResourceId: resourceId,
            ResourceName: resourceName,
            Details: details,
            HttpMethod: method,
            RequestPath: path,
            StatusCode: statusCode,
            CorrelationId: httpContext.TraceIdentifier,
            LogGroup: logGroup,
            ActorUnitId: actorUnitId,
            ActorFullName: actorFullName);

        _auditPublisher.Publish(auditEvent);
    }

    /// <summary>
    /// Mặc định: thao tác liên quan Hồ sơ/Hồ sơ số hóa thuộc tab "Nhật ký nghiệp vụ", còn lại thuộc "Nhật ký thao tác".
    /// Controller xử lý tài liệu trong hồ sơ có thể override qua AuditContext.SetAudit(logGroup: ...).
    /// </summary>
    private static string ResolveLogGroup(string resourceType)
    {
        return resourceType.StartsWith("DOSSIER", StringComparison.OrdinalIgnoreCase)
            ? AuditLogGroups.Business
            : AuditLogGroups.Operation;
    }

    private static bool IsSkipped(ActionExecutedContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.Any(em => em is SkipAuditAttribute))
            return true;

        if (context.ActionDescriptor is ControllerActionDescriptor controllerAction)
        {
            if (Attribute.IsDefined(controllerAction.ControllerTypeInfo, typeof(SkipAuditAttribute), inherit: true))
                return true;
        }

        return false;
    }

    private static string? ExtractResourceId(ActionExecutedContext context)
    {
        string[] keys = ["id", "dossierId", "documentId", "userId", "roleId", "folderId", "equipmentId", "versionId"];
        foreach (var key in keys)
        {
            if (context.RouteData.Values.TryGetValue(key, out var value) && value is not null)
            {
                var text = value.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return null;
    }

    /// <summary>
    /// Mã hồ sơ (CODE) là dữ liệu EAV động, không phải property C# tĩnh — nằm trong key "CODE" (không phân biệt
    /// hoa/thường) của JSON <c>FormDataJson</c>. Ưu tiên giá trị đã bắt được từ request (CaptureRequestCode),
    /// fallback quét response nếu action trả về object có FormDataJson (vd GetDetail). Không tìm thấy → null
    /// (BuildDefaultDetails sẽ fallback về ResourceId).
    /// </summary>
    private static string? ExtractCodeFromContext(ActionExecutedContext context)
    {
        if (context.HttpContext.Items.TryGetValue("__AuditRequestCode", out var requestCode) &&
            requestCode is string requestCodeStr && !string.IsNullOrWhiteSpace(requestCodeStr))
        {
            return requestCodeStr;
        }

        if (context.Result is ObjectResult { Value: not null } objectResult)
        {
            var code = TryGetCodeFromFormDataJson(objectResult.Value);
            if (!string.IsNullOrWhiteSpace(code))
                return code;
        }

        return null;
    }

    private static string? TryGetCodeFromFormDataJson(object? value)
    {
        if (value is null)
            return null;

        var property = value.GetType().GetProperty("FormDataJson");
        if (property?.GetValue(value) is not string formDataJson || string.IsNullOrWhiteSpace(formDataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(formDataJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var jsonProperty in root.EnumerateObject())
            {
                if (!string.Equals(jsonProperty.Name, "CODE", StringComparison.OrdinalIgnoreCase))
                    continue;

                var code = jsonProperty.Value.ValueKind switch
                {
                    JsonValueKind.String => jsonProperty.Value.GetString(),
                    JsonValueKind.Number => jsonProperty.Value.ToString(),
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(code))
                    return code;
            }
        }
        catch (JsonException)
        {
            // FormDataJson không hợp lệ — bỏ qua, fallback ResourceId.
        }

        return null;
    }

    private static string? ExtractLoginUsername(ActionExecutedContext context)
    {
        return context.HttpContext.Items.TryGetValue("__AuditLoginUsername", out var username)
            ? username?.ToString()
            : null;
    }

    private static string BuildDefaultDetails(
        string action,
        string resourceType,
        string? resourceName,
        string? resourceId)
    {
        var actionLabel = AuditVietnameseLabels.GetActionLabel(action);
        var resourceLabel = AuditVietnameseLabels.GetResourceTypeLabel(resourceType);
        var target = !string.IsNullOrWhiteSpace(resourceName)
            ? resourceName
            : !string.IsNullOrWhiteSpace(resourceId)
                ? resourceId
                : null;

        return string.IsNullOrWhiteSpace(target)
            ? $"{actionLabel} {resourceLabel}"
            : $"{actionLabel} {resourceLabel}: {target}";
    }
}
