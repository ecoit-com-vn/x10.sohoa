namespace EvnHanoi.EquipmentService.Core.DTOs;

/// <summary>1 dòng import Kệ/Tầng/Hộp bị lỗi — trả về cho người dùng biết dòng nào, sheet nào, vì sao.</summary>
public class PhysicalStorageImportErrorDto
{
    public string Sheet { get; set; } = string.Empty;
    public int Row { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class PhysicalStorageImportResultDto
{
    public int ShelvesCreated { get; set; }
    public int FloorsCreated { get; set; }
    public int BoxesCreated { get; set; }
    public List<PhysicalStorageImportErrorDto> Errors { get; set; } = new();
}
