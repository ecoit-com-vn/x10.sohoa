namespace EvnHanoi.SyncService.Models.Pmis;

// Tên field khớp đúng tài liệu "[EVNHANOI_SHHSKT] Phương án đồng bộ PMIS" —
// PmisClient bật PropertyNameCaseInsensitive nên PascalCase ở đây map thẳng sang camelCase PMIS.

/// <summary>Tiêu chí lọc — API 1: Danh sách TBA.</summary>
public class PmisSubstationSearchRequest
{
    public string? MaDonVi { get; set; }
    public int? LoaiTBA { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}

/// <summary>Tiêu chí lọc — API 2: Danh sách đường dây.</summary>
public class PmisLineSearchRequest
{
    public string? MaDonVi { get; set; }
    public int? MaLoaiDuongDay { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}

/// <summary>Tiêu chí lọc — API 3 &amp; 5: Danh sách loại thiết bị (TBA hoặc đường dây, cùng shape).</summary>
public class PmisDeviceTypeSearchRequest
{
    public string? MaLoaiTB { get; set; }
    public string? TenLoaiTB { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}

/// <summary>Tiêu chí lọc — API 4: Danh sách thiết bị TBA.</summary>
public class PmisSubstationDeviceSearchRequest
{
    public string? MaTBA { get; set; }
    public string? MaLoaiTB { get; set; }
    public string? MaDonVi { get; set; }
    public int? NamSanXuat { get; set; }
    public int? TinhTrang { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}

/// <summary>Tiêu chí lọc — API 6: Danh sách thiết bị đường dây.</summary>
public class PmisLineDeviceSearchRequest
{
    public string? MaDuongDay { get; set; }
    public string? MaLoaiTB { get; set; }
    public string? MaDonVi { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    // Có kèm ảnh QR code (base64) trong response hay không — xác nhận có thật qua Kong route export
    // và Postman collection thực tế (?kemQRCode=false), dù bảng "Đầu vào" trong docx PMIS bỏ sót field này.
    public bool? KemQRCode { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}

/// <summary>Tiêu chí lọc — API 7: Chi tiết thiết bị.</summary>
public class PmisDeviceDetailRequest
{
    public string MaThietBi { get; set; } = string.Empty;
    public string? MaTBA { get; set; }
}

/// <summary>Tiêu chí lọc — API ảnh QR (ngoài 9 API gốc, phát hiện thêm khi test thật).</summary>
public class PmisDeviceQrImageRequest
{
    public string IdPmis { get; set; } = string.Empty;
}

/// <summary>Tiêu chí lọc — API 8: Danh sách tài liệu thiết bị TBA.</summary>
public class PmisSubstationDocumentSearchRequest
{
    public string? MaTBA { get; set; }
    public string? MaLoaiTB { get; set; }
    public string? MaTB { get; set; }
    public string? MaDonVi { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}

/// <summary>Tiêu chí lọc — API 9: Danh sách tài liệu thiết bị đường dây.</summary>
public class PmisLineDocumentSearchRequest
{
    public string? MaDuongDay { get; set; }
    public string? MaLoaiTB { get; set; }
    public string? MaTB { get; set; }
    public string? MaDonVi { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}
