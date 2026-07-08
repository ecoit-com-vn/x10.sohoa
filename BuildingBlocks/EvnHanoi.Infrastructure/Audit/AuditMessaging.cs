namespace EvnHanoi.Infrastructure.Audit;

/// <summary>
/// Nhật ký thao tác người dùng trên Elasticsearch (không phải app_logs Serilog).
/// </summary>
public static class AuditMessaging
{
    public const string QueueName = "audit_event_queue";
    public const string IndexPrefix = "audit_logs";
}
