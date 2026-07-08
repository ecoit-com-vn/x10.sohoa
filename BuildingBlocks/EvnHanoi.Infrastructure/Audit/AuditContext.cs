using Microsoft.AspNetCore.Http;

namespace EvnHanoi.Infrastructure.Audit;

public sealed class AuditContextData
{
    public string? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string? Details { get; set; }
    public string? Action { get; set; }
    public string? ResourceType { get; set; }
}

public static class AuditContext
{
    private const string HttpContextKey = "EvnHanoi.AuditContext";

    public static void Set(HttpContext httpContext, AuditContextData data)
    {
        httpContext.Items[HttpContextKey] = data;
    }

    public static AuditContextData? Get(HttpContext? httpContext)
    {
        if (httpContext?.Items.TryGetValue(HttpContextKey, out var value) == true && value is AuditContextData data)
            return data;

        return null;
    }
}
