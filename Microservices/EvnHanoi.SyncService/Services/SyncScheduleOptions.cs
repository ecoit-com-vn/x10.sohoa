namespace EvnHanoi.SyncService.Services;

/// <summary>Cấu hình lịch đồng bộ PMIS tự động — section "Pmis:Schedule" (biến môi trường Pmis__Schedule__*).
/// Dùng chung cho Trạm biến áp, Đường dây và Thiết bị.</summary>
public class SyncScheduleOptions
{
    public const string SectionName = "Pmis:Schedule";

    /// <summary>Tần suất đồng bộ tối thiểu (phút) admin được đặt ở màn Thiết lập lịch — mặc định 2 giờ. Dòng cấu hình
    /// cũ đặt thấp hơn mức này vẫn chạy với mức tối thiểu (xem <see cref="SyncRunBudget.EffectiveFrequency"/>).</summary>
    public int MinFrequencyMinutes { get; set; } = 120;

    /// <summary>Phần đệm (phút) trừ khỏi tần suất để ra thời gian chạy tối đa của 1 lượt:
    /// ngân sách = tần suất − đệm. Bù cho lần gọi PMIS dở lúc hết hạn (timeout HTTP tối đa 5 phút), lưu trang cuối và ghi lịch sử.</summary>
    public int RunBudgetBufferMinutes { get; set; } = 10;
}
