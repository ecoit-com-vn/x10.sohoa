using EvnHanoi.SyncService.Models;

namespace EvnHanoi.SyncService.Services;

/// <summary>Tuỳ chọn quét tài liệu của 1 owner (Trạm/Đường dây/Thiết bị).</summary>
public class DocumentScanOptions
{
    /// <summary>Chỉ lấy tài liệu từ ngày này (PMIS lọc phía server) — null = không lọc.</summary>
    public DateTime? TuNgay { get; set; }

    /// <summary>Chỉ lấy tài liệu đến ngày này — null = không lọc.</summary>
    public DateTime? DenNgay { get; set; }

    /// <summary>Vị trí skip bắt đầu (tiếp tục quét dở ở lượt trước).</summary>
    public int StartSkip { get; set; }

    /// <summary>true (mặc định, luồng Manual/inline): tính vào trần MaxDocumentSyncCallsPerRun. Job DOCUMENT dùng ngân sách thời gian nên đặt false.</summary>
    public bool UseCallBudget { get; set; } = true;

    /// <summary>Hỏi trước mỗi trang: true → dừng mềm (hết ngân sách thời gian), <see cref="DocumentOwnerSyncResult.NextSkip"/> cho biết chỗ tiếp tục.</summary>
    public Func<bool>? ShouldStop { get; set; }
}

/// <summary>Kết quả đồng bộ tài liệu của 1 owner (hoặc 1 khoảng ngày của owner).</summary>
public class DocumentOwnerSyncResult
{
    public int Warnings { get; set; }
    public List<SyncHistoryDetail> Details { get; } = [];

    /// <summary>Số tài liệu PMIS trả về (đã nhận) trong lượt này.</summary>
    public int Received { get; set; }

    /// <summary>Số tài liệu mới tạo thật sự (chưa có trong DB).</summary>
    public int Created { get; set; }

    /// <summary>Số tài liệu lưu lỗi.</summary>
    public int Failed { get; set; }

    /// <summary>Tổng PMIS báo ở trang cuối (theo bộ lọc ngày nếu có).</summary>
    public int RemoteTotal { get; set; }

    /// <summary>Quét hết danh sách (tới trang cuối).</summary>
    public bool Completed { get; set; }

    /// <summary>Dừng giữa chừng vì <see cref="DocumentScanOptions.ShouldStop"/> (hết ngân sách thời gian).</summary>
    public bool Stopped { get; set; }

    /// <summary>Dừng vì chạm giới hạn an toàn số bản ghi/lượt — còn tài liệu chưa lấy.</summary>
    public bool Truncated { get; set; }

    /// <summary>Quét dừng vì circuit breaker PMIS đang mở (không phải lỗi của owner) — job tạm dừng cả lượt.</summary>
    public bool CircuitOpen { get; set; }

    /// <summary>Mọi tài liệu của lượt này đều lưu lỗi (vd EquipmentService ngừng) — coi như lỗi, không đánh dấu tiến triển.</summary>
    public bool AllFailed => Received > 0 && Failed >= Received;

    /// <summary>Lỗi (gọi PMIS...) làm dừng quét; null nếu không.</summary>
    public string? Error { get; set; }

    /// <summary>Vị trí skip để tiếp tục lượt sau.</summary>
    public int NextSkip { get; set; }

    public void Deconstruct(out int warnings, out List<SyncHistoryDetail> details)
    {
        warnings = Warnings;
        details = Details;
    }
}
