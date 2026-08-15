using System;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class OrganizationUnit
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? OrgIdSso { get; set; }
    public long? ParentId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
}
