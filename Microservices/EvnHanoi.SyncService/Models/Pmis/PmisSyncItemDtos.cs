namespace EvnHanoi.SyncService.Models.Pmis;

// Field đã xác nhận lại theo response THẬT gọi trực tiếp vào gateway PMIS đang chạy
// (https://dev-api-gateway.bzkiap.com, xem BAO_CAO_TEST_API_PMIS_GATEWAY_THAT.md + pmis-api-responses/*.json) —
// không còn dựa vào tài liệu docx (từng có mâu thuẫn nội bộ giữa bảng mô tả field và ví dụ JSON).

/// <summary>Item — API 1: Danh sách TBA.</summary>
public class PmisSubstationDto
{
    public string MaTBA { get; set; } = string.Empty;
    public string TenTBA { get; set; } = string.Empty;
    public string? MaDonVi { get; set; }
    public string? TenDonVi { get; set; }
    public string? CapDienAp { get; set; }
    public int? MaLoaiTBA { get; set; }
    public string? TenLoaiTBA { get; set; }
    public string? DiaDiem { get; set; }
    public DateTime? NgayVanHanh { get; set; }
    public int? TrangThai { get; set; }
}

/// <summary>Item — API 2: Danh sách đường dây.</summary>
public class PmisLineDto
{
    public string MaDuongDay { get; set; } = string.Empty;
    public string TenDuongDay { get; set; } = string.Empty;
    public string? CapDienAp { get; set; }
    public int? MaLoaiDuongDay { get; set; }
    public string? TenLoaiDuongDay { get; set; }
    public double? ChieuDai { get; set; }
    public string? DiemDau { get; set; }
    public string? DiemCuoi { get; set; }
    public string? MaDonVi { get; set; }
    public string? TenDonVi { get; set; }
    public DateTime? NgayVanHanh { get; set; }
    public int? TrangThai { get; set; }
}

/// <summary>Item — API 3 &amp; 5: Loại thiết bị (TBA hoặc đường dây).</summary>
public class PmisDeviceTypeDto
{
    public string MaLoaiTB { get; set; } = string.Empty;
    public string TenLoaiTB { get; set; } = string.Empty;
}

/// <summary>
/// Item — API 4: Thiết bị TBA. Schema THẬT khác hẳn thiết bị đường dây (không dùng chung field
/// maTB/tenTB, không có maQRCode/thongSoKyThuat trong danh sách — phải gọi thêm ChiTietThietBi,
/// xem PmisSyncExecutionService.SyncEquipmentAsync).
/// </summary>
public class PmisSubstationDeviceDto
{
    public string MaThietBi { get; set; } = string.Empty;
    public string TenThietBi { get; set; } = string.Empty;
    public string? MaLoaiTB { get; set; }
    public string? TenLoaiTB { get; set; }
    public string? MaTBA { get; set; }
    public string? TenTBA { get; set; }
    public string? CapDienAp { get; set; }
    public string? Serial { get; set; }
    public string? Model { get; set; }
    public string? HangSanXuat { get; set; }
    public int? NamSanXuat { get; set; }
    public string? MaDonVi { get; set; }
    public string? TenDonVi { get; set; }
    public DateTime? NgayVanHanh { get; set; }
    public DateTime? NgayTao { get; set; }
    public int? TinhTrang { get; set; }
    public string? TenTinhTrang { get; set; }
}

/// <summary>Item — API 6: Thiết bị đường dây.</summary>
public class PmisLineDeviceDto
{
    public string MaTB { get; set; } = string.Empty;
    public string TenTB { get; set; } = string.Empty;
    public string? MaLoaiTB { get; set; }
    public string? TenLoaiTB { get; set; }
    public string? MaDuongDay { get; set; }
    public string? TenDuongDay { get; set; }
    public string? MaDonVi { get; set; }

    /// <summary>URL ảnh QR (vd. ".../AnhQRCode?idPmis=..."), KHÔNG PHẢI base64 — phải tải ảnh thật rồi
    /// tự encode base64 khi lưu (xem PmisSyncExecutionService.SyncEquipmentAsync).</summary>
    public string? MaQRCode { get; set; }
    public int? MaTrangThai { get; set; }
    public int? NamSanXuat { get; set; }
    public string? TrangThai { get; set; }
    public string? ThongSoKyThuat { get; set; }
}

/// <summary>Item — API 7: Chi tiết thiết bị (dùng chung cho cả thiết bị TBA và đường dây).</summary>
public class PmisDeviceDetailDto
{
    public string MaTB { get; set; } = string.Empty;
    public string TenTB { get; set; } = string.Empty;
    public string? MaLoaiTB { get; set; }
    public string? TenLoaiTB { get; set; }
    public string? MaTBA { get; set; }
    public string? TenTBA { get; set; }
    public string? MaDonVi { get; set; }
    public int? NamSanXuat { get; set; }

    /// <summary>URL ảnh QR — xem ghi chú tại <see cref="PmisLineDeviceDto.MaQRCode"/>.</summary>
    public string? MaQRCode { get; set; }
    public string? TrangThai { get; set; }
    public string? ThongSoKyThuat { get; set; }
}

/// <summary>Item — API 8: Tài liệu thiết bị TBA.</summary>
public class PmisSubstationDocumentDto
{
    public string? MaTB { get; set; }
    public string? TenTB { get; set; }
    public string? MaLoaiTB { get; set; }
    public string? TenLoaiTB { get; set; }
    public string? MaTBA { get; set; }
    public string? TenTBA { get; set; }
    public string? MaDonVi { get; set; }
    public string MaTaiLieu { get; set; } = string.Empty;
    public string? TenTaiLieu { get; set; }
    public string? LoaiTaiLieu { get; set; }
    public string? File { get; set; }
}

/// <summary>Item — API 9: Tài liệu thiết bị đường dây.</summary>
public class PmisLineDocumentDto
{
    public string? MaDuongDay { get; set; }
    public string? TenDuongDay { get; set; }
    public string? MaTB { get; set; }
    public string? TenTB { get; set; }
    public string? MaLoaiTB { get; set; }
    public string? TenLoaiTB { get; set; }
    public string? MaDonVi { get; set; }
    public string MaTaiLieu { get; set; } = string.Empty;
    public string? TenTaiLieu { get; set; }
    public string? LoaiTaiLieu { get; set; }
    public DateTime? NgayTaiLieu { get; set; }
    public string? File { get; set; }
}
