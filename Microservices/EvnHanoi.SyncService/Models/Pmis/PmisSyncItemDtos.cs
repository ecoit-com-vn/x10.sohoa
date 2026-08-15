namespace EvnHanoi.SyncService.Models.Pmis;

// Field theo đúng ví dụ Response trong tài liệu PMIS — dùng ví dụ JSON làm chuẩn khi tài liệu
// có chênh lệch nhẹ giữa bảng mô tả field và ví dụ (vd. API loại thiết bị: bảng ghi maLoaiTB/tenLoaiTB
// nhưng ví dụ response lại là maLoai/tenLoai).

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
    public string MaLoai { get; set; } = string.Empty;
    public string TenLoai { get; set; } = string.Empty;
}

/// <summary>Item — API 4: Thiết bị TBA.</summary>
public class PmisSubstationDeviceDto
{
    public string MaTB { get; set; } = string.Empty;
    public string TenTB { get; set; } = string.Empty;
    public string? MaLoaiTB { get; set; }
    public string? TenLoaiTB { get; set; }
    public string? MaTBA { get; set; }
    public string? TenTBA { get; set; }
    public string? MaDonVi { get; set; }
    public int? NamSanXuat { get; set; }

    /// <summary>Base64 — lưu vào EQUIPMENTS.QR_CODE khi đồng bộ (module QRCode thiết bị).</summary>
    public string? MaQRCode { get; set; }
    public string? TrangThai { get; set; }

    /// <summary>Chuỗi JSON thông số kỹ thuật — lưu riêng vào EQUIPMENT_PMIS_SPEC, KHÔNG ghi đè FormValues.</summary>
    public string? ThongSoKyThuat { get; set; }
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
    public string? MaQRCode { get; set; }
    public int? MaTrangThai { get; set; }
    public int? NamSanXuat { get; set; }
    public string? TrangThai { get; set; }
    public string? ThongSoKyThuat { get; set; }
}

/// <summary>Item — API 7: Chi tiết thiết bị.</summary>
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
