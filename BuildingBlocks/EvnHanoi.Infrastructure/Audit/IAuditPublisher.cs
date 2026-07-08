namespace EvnHanoi.Infrastructure.Audit;

public interface IAuditPublisher
{
    void Publish(AuditEvent auditEvent);
}
