using System;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class Unit
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? ParentId { get; set; }
    public string Description { get; set; } = string.Empty;
}

