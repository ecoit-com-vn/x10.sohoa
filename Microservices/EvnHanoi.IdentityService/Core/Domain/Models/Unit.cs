using System;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class Unit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
}
