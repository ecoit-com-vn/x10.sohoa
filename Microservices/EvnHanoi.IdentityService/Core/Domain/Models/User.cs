using System;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class User
{
    public string Id { get; set; } = string.Empty; // UUID v7
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public long? OrganizationUnitId { get; set; }
    public bool IsActive { get; set; } = true;
    public int AccessFailedCount { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; } = true;

    // Chi tiết Đơn vị liên kết hiển thị lên FE
    public OrganizationUnit? OrganizationUnit { get; set; }
}
