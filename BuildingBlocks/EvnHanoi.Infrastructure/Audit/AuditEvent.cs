namespace EvnHanoi.Infrastructure.Audit;

public record AuditEvent(
    string Id,
    DateTime OccurredAt,
    string ServiceName,
    string ActorUserId,
    string ActorUserName,
    string? ActorIp,
    string Action,
    string ResourceType,
    string? ResourceId,
    string? ResourceName,
    string? Details,
    string? HttpMethod,
    string? RequestPath,
    int? StatusCode,
    string? CorrelationId,
    bool IsDeleted = false);

public static class AuditActions
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string Manage = "MANAGE";
    public const string Import = "IMPORT";
    public const string Export = "EXPORT";
    public const string Release = "RELEASE";
    public const string Login = "LOGIN";
    public const string Logout = "LOGOUT";
}
