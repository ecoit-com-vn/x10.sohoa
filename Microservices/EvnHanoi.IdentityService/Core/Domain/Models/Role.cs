using System;
using EvnHanoi.Infrastructure.Enums;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class Role
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long ScopeTypeId { get; set; } = 1;
    public string ScopeTypeName { get; set; } = string.Empty;
    public long? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
}
public static class RoleScopeTypes
{
    public static readonly GenericEnumItem GLOBAL = new(1, "GLOBAL", "Nhóm quyền hệ thống");
    public static readonly GenericEnumItem UNIT = new(2, "UNIT", "Nhóm quyền đơn vị");

    private static readonly GenericEnumItem[] All = { GLOBAL, UNIT };
}

