using System;

namespace EvnHanoi.EquipmentService.Core.Entities;

public class Country
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
