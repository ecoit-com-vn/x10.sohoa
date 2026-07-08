namespace EvnHanoi.Infrastructure.Audit;

public sealed class AuditServiceMetadata
{
    public AuditServiceMetadata(string serviceName)
    {
        ServiceName = serviceName;
    }

    public string ServiceName { get; }
}
