namespace EvnHanoi.SyncService.Services;

/// <summary>Cấu hình đồng bộ PMIS tăng dần — section "Pmis:IncrementalSync" (biến môi trường Pmis__IncrementalSync__*).
/// Tắt (mặc định) = hành vi cũ: mỗi lượt quét và đẩy lại toàn bộ.</summary>
public class PmisIncrementalOptions
{
    public const string SectionName = "Pmis:IncrementalSync";

    public bool Enabled { get; set; }

    /// <summary>Sau bao nhiêu giờ kể từ lượt quét đầy đủ gần nhất thì lượt kế tiếp bỏ qua hash, đẩy lại toàn bộ
    /// (mặc định 7 ngày) — bù cho thay đổi mà hash không phát hiện (vd. chi tiết thiết bị, tài liệu đính kèm).</summary>
    public int FullSweepIntervalHours { get; set; } = 168;

    /// <summary>Số lần gọi PMIS lấy chi tiết (ChiTietThietBi + ảnh QR) tối đa trong 1 lượt đồng bộ Thiết bị (mặc định 2000). Việc lấy chi tiết
    /// chạy ở pha "bổ sung chi tiết" RIÊNG sau pha quét cha — hết trần/hết giờ thì dừng, lượt sau làm tiếp; KHÔNG còn chặn việc quét cha.</summary>
    public int EquipmentDetailCallsPerRun { get; set; } = 2000;

    /// <summary>Tỷ lệ ngân sách thời gian của lượt dành cho pha QUÉT CHA khi còn thiết bị chờ bổ sung chi tiết (phần còn lại dành cho pha
    /// bổ sung chi tiết). Mặc định 0,75. Không còn thiết bị chờ thì pha quét cha dùng hết ngân sách.</summary>
    public double ParentScanTimeShare { get; set; } = 0.75;

    /// <summary>Trạm/Đường dây cha không đổi chỉ được quét lại thiết bị con sau khoảng này.</summary>
    public int ParentRescanIntervalHours { get; set; } = 24;

    /// <summary>Tăng số này khi đổi cách map/băm dữ liệu để buộc đẩy lại toàn bộ 1 lần.</summary>
    public int HashVersion { get; set; } = 1;
}
