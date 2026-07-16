using System.Collections.Generic;

namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class PermissionGroup
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long ScopeTypeId { get; set; } = 1;
    public string GroupType { get; set; } = "SYSTEM";
    public string ScopeTypeName { get; set; } = string.Empty;
    /// <summary>Giữ cột cũ (đơn vị đầu tiên) để tương thích; nguồn chân lý là OrganizationUnitIds.</summary>
    public long? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    /// <summary>Danh sách đơn vị gắn với nhóm quyền đơn vị (mapping).</summary>
    public List<long> OrganizationUnitIds { get; set; } = new();
    /// <summary>Tên các đơn vị (LISTAGG) để hiển thị danh sách.</summary>
    public string? OrganizationUnitNames { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class PermissionGroupTypes
{
    public const string System = "SYSTEM";
    public const string Unit = "UNIT";
}
