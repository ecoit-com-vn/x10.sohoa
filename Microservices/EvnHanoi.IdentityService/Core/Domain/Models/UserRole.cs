using System;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}
