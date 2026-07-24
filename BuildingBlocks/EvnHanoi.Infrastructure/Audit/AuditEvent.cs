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
    bool IsDeleted = false,
    string LogGroup = AuditLogGroups.Operation,
    string? ActorUnitId = null,
    string? ActorUnitName = null,
    string? ActorFullName = null);

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

/// <summary>
/// Phân nhóm tab hiển thị ở màn Nhật ký hệ thống: thao tác chung vs nghiệp vụ hồ sơ/tài liệu.
/// </summary>
public static class AuditLogGroups
{
    public const string Operation = "THAO_TAC";
    public const string Business = "NGHIEP_VU";
}
