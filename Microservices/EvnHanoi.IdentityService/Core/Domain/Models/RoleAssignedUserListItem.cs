namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class RoleAssignedUserListItem
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public string? OrganizationUnitName { get; set; }
}
