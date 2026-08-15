using System.Text.Json;

namespace EvnHanoi.SyncService.Models;

/// <summary>
/// Tiêu chí tìm kiếm gộp chung cho cả 3 đối tượng (Trạm/Đường dây/Thiết bị) — field nào không áp
/// dụng cho đối tượng đang chọn thì bỏ qua. Với Thiết bị: truyền đúng 1 trong 2 MaTBA/MaDuongDay
/// để biết tìm thiết bị của trạm hay của đường dây (đúng theo 2 API khác nhau trong tài liệu PMIS).
/// </summary>
public class PmisManualSearchRequest
{
    public string? MaDonVi { get; set; }
    public int? LoaiTBA { get; set; }
    public int? MaLoaiDuongDay { get; set; }
    public string? MaTBA { get; set; }
    public string? MaDuongDay { get; set; }
    public string? MaLoaiTB { get; set; }
    public int? NamSanXuat { get; set; }
    public int? TinhTrang { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}

/// <summary>1 dòng preview trả về FE — giữ nguyên RawData để FE gửi lại y hệt khi Lưu (không fetch lại PMIS).</summary>
public class PmisSyncPreviewItemDto
{
    public string PmisCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public JsonElement RawData { get; set; }
}

public class PmisManualSearchResponse
{
    public int Total { get; set; }
    public List<PmisSyncPreviewItemDto> Items { get; set; } = [];
}

public class PmisManualSaveRequest
{
    public List<JsonElement> Items { get; set; } = [];
}

public class PmisManualSaveResponse
{
    public string SyncHistoryId { get; set; } = string.Empty;
    public int Total { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = [];
}
