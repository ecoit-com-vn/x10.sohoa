using System;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class UserGroupPermission
{
    public long UserGroupId { get; set; }
    public string PermissionId { get; set; } = string.Empty;
}
