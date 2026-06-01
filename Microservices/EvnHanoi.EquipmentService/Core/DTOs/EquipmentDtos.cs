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
    
    public Dictionary<Guid, string> DynamicAttributes { get; set; } = new();
}

public class EquipmentDto
{
    public Guid Id { get; set; }
    public Guid EquipmentTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public long? UnitId { get; set; }
    
    public Dictionary<Guid, string> DynamicAttributes { get; set; } = new();
}
