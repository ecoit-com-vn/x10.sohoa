using System;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? UnitId { get; set; }
    public bool IsActive { get; set; } = true;
}
