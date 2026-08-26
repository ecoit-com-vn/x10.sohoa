namespace EvnHanoi.EquipmentService.Core.DTOs;

// Ánh xạ mã loại thiết bị PMIS (maLoaiTB) + cấp lưới điện → loại thiết bị hệ thống.
// Xem Migration0052_CreatePmisEquipmentTypeMapping để biết vì sao không so khớp trực tiếp được.

public class PmisEquipmentTypeMappingDto
{
    public string Id { get; set; } = string.Empty;
    public string PmisMaLoaiTB { get; set; } = string.Empty;
    public int GridTypeId { get; set; }
    public string? GridTypeName { get; set; }
    public string EquipmentTypeId { get; set; } = string.Empty;
    public string? EquipmentTypeCode { get; set; }
    public string? EquipmentTypeName { get; set; }
    public int RowVersion { get; set; }
}

public class SavePmisEquipmentTypeMappingRequest
{
    public string PmisMaLoaiTB { get; set; } = string.Empty;
    public int GridTypeId { get; set; }
    public string EquipmentTypeId { get; set; } = string.Empty;

    /// <summary>Chỉ dùng khi sửa — khoá lạc quan, lệch phiên bản thì trả 409.</summary>
    public int? RowVersion { get; set; }
}
