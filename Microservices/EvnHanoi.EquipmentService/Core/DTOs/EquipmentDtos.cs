// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.EquipmentService\Core\DTOs\EquipmentDtos.cs
using System;
using System.Collections.Generic;

namespace EvnHanoi.EquipmentService.Core.DTOs;

public class EquipmentCreateDto
{
    public Guid EquipmentTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public long? UnitId { get; set; }
    public Guid? InfrastructureId { get; set; }
    public Guid? CountryId { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Key: AttributeDefinitionId, Value: string
    public Dictionary<Guid, string> DynamicAttributes { get; set; } = new();
}

public class EquipmentUpdateDto
{
    public Guid EquipmentTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public long? UnitId { get; set; }
    public Guid? InfrastructureId { get; set; }
    public Guid? CountryId { get; set; }
    public bool IsActive { get; set; }
    public string? FormValues { get; set; }
    public Dictionary<Guid, string> DynamicAttributes { get; set; } = new();
}

public class EquipmentDto
{
    public Guid Id { get; set; }
    public Guid EquipmentTypeId { get; set; }
    public string EquipmentTypeName { get; set; } = string.Empty;
    public string EquipmentTypeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public Guid? InfrastructureId { get; set; }
    public string InfrastructureName { get; set; } = string.Empty;
    public string InfrastructureCode { get; set; } = string.Empty;
    public long? UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public int? GridTypeId { get; set; }
    public string GridTypeName { get; set; } = string.Empty;
    public Guid? CountryId { get; set; }
    public string CountryName { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public CreatorInfoDto? Creator { get; set; }
    
    // Audit logs
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? FormValues { get; set; }
    public Dictionary<Guid, string> DynamicAttributes { get; set; } = new();
    public string? FormTemplateName { get; set; }
    public string? FormSchema { get; set; }
}

public class EquipmentTypeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int GridTypeId { get; set; }
    public string GridTypeName { get; set; } = string.Empty;
    public int? SortOrder { get; set; }
    public bool IsActive { get; set; }
    public CreatorInfoDto? Creator { get; set; }
    
    // Audit logs
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
