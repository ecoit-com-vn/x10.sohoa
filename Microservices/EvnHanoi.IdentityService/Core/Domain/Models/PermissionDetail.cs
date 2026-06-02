using System;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class PermissionDetail
{
    public string Id { get; set; } = string.Empty;
    public string PermissionId { get; set; } = string.Empty;
    public string ControllerName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
}
