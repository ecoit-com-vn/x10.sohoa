using System.Text.Json.Serialization;

namespace EvnHanoi.EquipmentService.Core.DTOs;

public class PmisEquipmentListRequestDto
{
    [JsonPropertyName("maTB")]
    public string? MaTB { get; set; }

    [JsonPropertyName("maTram")]
    public string? MaTram { get; set; }

    [JsonPropertyName("maDuongday")]
    public string? MaDuongDay { get; set; }

    [JsonPropertyName("maLoaiTB")]
    public string? MaLoaiTB { get; set; }

    [JsonPropertyName("maDonVi")]
    public string? MaDonVi { get; set; }

    [JsonPropertyName("tuNgay")]
    public DateTime? TuNgay { get; set; }

    [JsonPropertyName("denNgay")]
    public DateTime? DenNgay { get; set; }

    [JsonPropertyName("loai")]
    public int? Loai { get; set; }

    [JsonPropertyName("skip")]
    public int Skip { get; set; } = 0;

    [JsonPropertyName("take")]
    public int Take { get; set; } = 100;

}
