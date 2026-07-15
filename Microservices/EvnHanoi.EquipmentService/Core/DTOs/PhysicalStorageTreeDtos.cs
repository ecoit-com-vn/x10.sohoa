namespace EvnHanoi.EquipmentService.Core.DTOs;

/// <summary>Cây kho lưu trữ: Kệ → Tầng → Hộp (theo đúng 1 đơn vị).</summary>
public class PhysicalStorageTreeShelfDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
    public List<PhysicalStorageTreeFloorDto> Floors { get; set; } = new();
}

public class PhysicalStorageTreeFloorDto
{
    public long Id { get; set; }
    public long ShelfId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
    public List<PhysicalStorageTreeBoxDto> Boxes { get; set; } = new();
}

public class PhysicalStorageTreeBoxDto
{
    public long Id { get; set; }
    public long FloorId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
}
