using System;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class RolePermission
{
    public string Id { get; set; } = string.Empty;
    public long RoleId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
}
