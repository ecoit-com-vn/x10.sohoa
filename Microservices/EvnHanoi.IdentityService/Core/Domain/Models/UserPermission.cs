using System;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class UserPermission
{
    public string UserId { get; set; } = string.Empty;
    public string PermissionId { get; set; } = string.Empty;
}
