namespace EvnHanoi.Infrastructure.Audit;

/// <summary>
/// Bỏ qua ghi audit tự động cho endpoint (health, lookup nội bộ...).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SkipAuditAttribute : Attribute;
