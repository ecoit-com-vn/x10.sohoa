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

    /// <summary>
    /// FK tới CATALOG.Id (EquipmentService) — Loại danh mục "Chức vụ" (Code = CHUC_VU)
    /// </summary>
    public long? PositionId { get; set; }

    /// <summary>
    /// Denormalized tên chức vụ — để hiển thị FE mà không cần cross-service join
    /// </summary>
    public string? PositionName { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public int AccessFailedCount { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; } = true;

    // Chi tiết Đơn vị liên kết hiển thị lên FE
    public OrganizationUnit? OrganizationUnit { get; set; }
}

