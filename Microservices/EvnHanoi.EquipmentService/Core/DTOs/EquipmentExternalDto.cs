using System.Text.Json.Serialization;

namespace EvnHanoi.EquipmentService.Core.DTOs;

public class EquipmentExternalDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("maTB")]
    public string MaTB { get; set; } = string.Empty;

    [JsonPropertyName("tenTB")]
    public string TenTB { get; set; } = string.Empty;

    [JsonPropertyName("maLoaiTB")]
    public string MaLoaiTB { get; set; } = string.Empty;

    [JsonPropertyName("tenLoaiTB")]
    public string TenLoaiTB { get; set; } = string.Empty;

    [JsonPropertyName("maTBA")]
    public string MaTBA { get; set; } = string.Empty;

    [JsonPropertyName("tenTBA")]
    public string TenTBA { get; set; } = string.Empty;

    [JsonPropertyName("maDonVi")]
    public string MaDonVi { get; set; } = string.Empty;

    [JsonPropertyName("namSanXuat")]
    public int NamSanXuat { get; set; }

    [JsonPropertyName("maQRCode")]
    public string MaQRCode { get; set; } = string.Empty;

    [JsonPropertyName("trangThai")]
    public string TrangThai { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;
}
