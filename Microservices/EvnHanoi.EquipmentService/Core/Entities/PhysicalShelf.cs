using System;

namespace EvnHanoi.EquipmentService.Core.Entities;

public class PhysicalShelf
{
    public long Id { get; set; }
    /// <summary>Đơn vị sở hữu kệ — bắt buộc (1 kệ thuộc đúng 1 đơn vị).</summary>
    public long? UnitId { get; set; }
    /// <summary>Tên đơn vị (join ORGANIZATION_UNIT, chỉ dùng khi đọc).</summary>
    public string? UnitName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; } = 1; // 1=Active,0=Locked
    public bool IsDeleted { get; set; } = false;
    public int Priority { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
