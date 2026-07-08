namespace EvnHanoi.Infrastructure.Audit;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Chỉ cho phép ghi audit_logs-* với thao tác từ người dùng thật (JWT).
/// Log hệ thống/worker → app_logs-* (Serilog), không qua đây.
/// </summary>
public static class AuditUserActionGuard
{
    public static readonly string[] NonUserActorIds = ["unknown", "system", "anonymous"];

    public static bool IsUserAction(AuditEvent auditEvent)
    {
        if (string.Equals(auditEvent.Action, AuditActions.Login, StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrWhiteSpace(auditEvent.ActorUserName);

        if (string.IsNullOrWhiteSpace(auditEvent.ActorUserId))
            return false;

        return !IsNonUserActor(auditEvent.ActorUserId);
    }

    public static bool ShouldSkipHttpAudit(
        HttpContext httpContext,
        bool isLogin,
        string actorUserId,
        string actorUserName)
    {
        var path = httpContext.Request.Path.Value ?? string.Empty;
        if (path.Contains("/health", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/internal/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/hubs/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (httpContext.Request.Headers.ContainsKey("X-Internal-Token"))
            return true;

        if (isLogin)
            return string.IsNullOrWhiteSpace(actorUserName);

        if (httpContext.User.Identity?.IsAuthenticated != true)
            return true;

        return IsNonUserActor(actorUserId);
    }

    private static bool IsNonUserActor(string actorUserId)
    {
        var trimmed = actorUserId.Trim();
        return NonUserActorIds.Any(id => string.Equals(trimmed, id, StringComparison.OrdinalIgnoreCase));
    }
}
