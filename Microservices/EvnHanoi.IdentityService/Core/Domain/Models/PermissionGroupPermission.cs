namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class PermissionGroupPermission
{
    public string Id { get; set; } = string.Empty;
    public long PermissionGroupId { get; set; }
    public string PermissionId { get; set; } = string.Empty;
}
