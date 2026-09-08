namespace EvnHanoi.EquipmentService.Core.Models;

/// <summary>
/// 1 trong 3 API tích hợp ký số ngoài (xem HUONG_DAN_TICH_HOP_KY_SO.md và KySoClient.cs) — Url/trạng
/// thái do admin quản lý qua màn "Thiết lập đồng bộ ký số". LƯU Ý: đây hiện là bảng theo dõi/tra
/// cứu — KySoClient vẫn đọc URL thực tế từ appsettings ("Endpoints:KySo"), chưa đọc từ bảng này.
/// </summary>
public class DigitalSignatureEndpointConfig
{
    public string Id { get; set; } = string.Empty;
    public string ApiCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsActive { get; set; }
    public int RowVersion { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public class DigitalSignatureEndpointConfigListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string ApiCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsActive { get; set; }
    public int RowVersion { get; set; }
}

public class UpdateDigitalSignatureEndpointConfigRequest
{
    public string? Url { get; set; }
    public bool IsActive { get; set; }
    public int RowVersion { get; set; }
}
