// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Core\Domain\Models\UserGroup.cs
using System;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class UserGroup
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
}
