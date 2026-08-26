using System.Collections.Generic;
using System.Text.Json;
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

/// <summary>
/// Danh sách thiết bị kèm "items" — dữ liệu form động (FORM_VALUES) của thiết bị,
/// ví dụ các trường ảnh đính kèm do front-end tự định nghĩa theo mẫu EAV của từng loại thiết bị.
/// </summary>
public class EquipmentDetailListDto
{
    /// <summary>Id nội bộ dùng để join dữ liệu, không trả ra ngoài.</summary>
    [JsonIgnore]
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

    [JsonPropertyName("trangThai")]
    public string TrangThai { get; set; } = string.Empty;

    /// <summary>Dữ liệu form động thô (JSON) đọc từ EQUIPMENTS.FORM_VALUES — dùng để dựng Items, không trả ra ngoài.</summary>
    [JsonIgnore]
    public string? FormValues { get; set; }

    /// <summary>Cấu trúc biểu mẫu EAV (JSON) của loại thiết bị — dùng để tách Items theo từng trường, không trả ra ngoài.</summary>
    [JsonIgnore]
    public string? FormSchema { get; set; }

    [JsonPropertyName("items")]
    public Dictionary<string, JsonElement> Items { get; set; } = new();
}
