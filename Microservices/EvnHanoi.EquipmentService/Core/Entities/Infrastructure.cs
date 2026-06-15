using System;

namespace EvnHanoi.EquipmentService.Core.Entities;

public class OrganizationDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? ParentId { get; set; }
}

public class Infrastructure
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int InfraTypeId { get; set; }
    public long? UnitId { get; set; }
    public bool IsActive { get; set; } = true;

    // Join helper fields
    public string? InfraTypeName { get; set; }
    public string? UnitName { get; set; }

    // Nested Organization DTO
    public OrganizationDto? Organization { get; set; }

    // Audit fields
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; } = false;
}
