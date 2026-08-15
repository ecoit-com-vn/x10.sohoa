using System;

namespace EvnHanoi.EquipmentService.Core.Entities;

public class OrganizationDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? ParentId { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    // Map helpers for Dapper matching Oracle SQL select aliases
    public long OrgId { get => Id; set => Id = value; }
    public string OrgCode { get => Code; set => Code = value; }
    public string OrgName { get => Name; set => Name = value; }
}

public class Infrastructure
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int InfraTypeId { get; set; }
    public long? UnitId { get; set; }
    public int? GridTypeId { get; set; }
    public DateTime? OperationDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Join helper fields
    public string? InfraTypeName { get; set; }
    public string? UnitName { get; set; }

    // Nested Organization DTO
    public OrganizationDto? Organization { get; set; }

    // Query helper: số thiết bị gắn với trạm/đường dây
    public int EquipmentCount { get; set; }

    // Audit fields
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; } = false;

    // Đồng bộ PMIS
    public string? PmisCode { get; set; }
    public DateTime? LastSyncedFromPmisAt { get; set; }
}
