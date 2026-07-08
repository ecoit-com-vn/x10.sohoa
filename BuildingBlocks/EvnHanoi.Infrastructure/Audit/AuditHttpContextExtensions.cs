using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.Infrastructure.Audit;

public static class AuditHttpContextExtensions
{
    public static void SetAudit(
        this HttpContext httpContext,
        string? resourceId = null,
        string? resourceName = null,
        string? details = null,
        string? resourceType = null,
        string? action = null)
    {
        AuditContext.Set(httpContext, new AuditContextData
        {
            ResourceId = resourceId,
            ResourceName = resourceName,
            Details = details,
            ResourceType = resourceType,
            Action = action
        });
    }

    public static void SetAudit(
        this ControllerBase controller,
        string? resourceId = null,
        string? resourceName = null,
        string? details = null,
        string? resourceType = null,
        string? action = null)
    {
        controller.HttpContext.SetAudit(resourceId, resourceName, details, resourceType, action);
    }
}
