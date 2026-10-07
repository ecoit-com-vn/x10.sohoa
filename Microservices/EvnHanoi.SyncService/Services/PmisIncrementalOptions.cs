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

    /// <summary>Trạm/Đường dây cha không đổi chỉ được quét lại thiết bị con sau khoảng này.</summary>
    public int ParentRescanIntervalHours { get; set; } = 24;

    /// <summary>Tăng số này khi đổi cách map/băm dữ liệu để buộc đẩy lại toàn bộ 1 lần.</summary>
    public int HashVersion { get; set; } = 1;
}
