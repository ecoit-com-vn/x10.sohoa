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
/// maTB/tenTB) — vẫn KHÔNG có maQRCode trong danh sách (phải gọi thêm ChiTietThietBi lấy QR, xem
/// PmisSyncExecutionService.SyncEquipmentAsync), nhưng PMIS đã bổ sung thêm thongSoKyThuat/
/// tenThongSoKyThuat NGAY TRONG danh sách này (xác nhận bằng test thật 2026-09-23, trước đó API này
/// không có 2 field này) — SyncEquipmentAsync vẫn giữ nguyên bước gọi ChiTietThietBi (còn cần cho QR)
/// và ưu tiên đè bằng kết quả ChiTietThietBi khi gọi thành công, chỉ dùng giá trị ở đây làm dự phòng.
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
    public string? ThongSoKyThuat { get; set; }

    /// <summary>Nhãn tiếng Việt cho từng khoá của <see cref="ThongSoKyThuat"/> (vd.
    /// {"I_DM":"Dòng điện định mức"}) — cùng bộ khoá, chỉ khác value là nhãn thay vì số liệu. PMIS mới
    /// bổ sung field này (2026-09-23), dùng để gợi ý nhãn thật cho admin khi khai "Tên trường PMIS"
    /// trong Form Builder thay vì phải đoán ý nghĩa khoá UPPER_SNAKE — xem
    /// EquipmentController.GetPmisSpecKeys.</summary>
    public string? TenThongSoKyThuat { get; set; }
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

    /// <summary>Xem ghi chú tại <see cref="PmisSubstationDeviceDto.TenThongSoKyThuat"/>.</summary>
    public string? TenThongSoKyThuat { get; set; }
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

    /// <summary>Xem ghi chú tại <see cref="PmisSubstationDeviceDto.TenThongSoKyThuat"/>.</summary>
    public string? TenThongSoKyThuat { get; set; }
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
