using System.Diagnostics;

namespace EvnHanoi.SyncService.Services;

/// <summary>
/// Thời gian chạy tối đa của 1 lượt đồng bộ tự động = tần suất cấu hình − phần đệm (vd tần suất 2 giờ, đệm 10 phút →
/// 110 phút). Thay cho các ngưỡng 60/35 phút cố định trước đây (SyncHistoryWatchdogJob, EquipmentRunBudgetMinutes).
/// Logic thuần (không I/O) để kiểm thử được.
/// </summary>
public static class SyncRunBudget
{
    /// <summary>Sàn tuyệt đối của ngân sách — dưới mức này 1 lượt gần như không làm được gì.</summary>
    public static readonly TimeSpan MinBudget = TimeSpan.FromMinutes(5);

    public static TimeSpan ToTimeSpan(int value, string unit) => unit switch
    {
        "MINUTE" => TimeSpan.FromMinutes(value),
        "HOUR" => TimeSpan.FromHours(value),
        "DAY" => TimeSpan.FromDays(value),
        _ => TimeSpan.FromMinutes(value)
    };

    /// <summary>Tần suất thực sự áp dụng: tần suất cấu hình nhưng không thấp hơn mức tối thiểu cho phép.</summary>
    public static TimeSpan EffectiveFrequency(int value, string unit, SyncScheduleOptions options)
    {
        var configured = ToTimeSpan(value, unit);
        var min = TimeSpan.FromMinutes(Math.Max(1, options.MinFrequencyMinutes));
        return configured < min ? min : configured;
    }

    /// <summary>true nếu tần suất cấu hình thấp hơn mức tối thiểu — dùng để từ chối ở API lưu cấu hình.</summary>
    public static bool IsBelowMinimum(int value, string unit, SyncScheduleOptions options) =>
        ToTimeSpan(value, unit) < TimeSpan.FromMinutes(Math.Max(1, options.MinFrequencyMinutes));

    /// <summary>Ngân sách thời gian của 1 lượt: tần suất hiệu lực − đệm; không nhỏ hơn
    /// max(<see cref="MinBudget"/>, tần suất/2) (để tần suất nhỏ không ra 0/âm) và không vượt quá tần suất.</summary>
    public static TimeSpan For(TimeSpan effectiveFrequency, SyncScheduleOptions options)
    {
        var buffer = TimeSpan.FromMinutes(Math.Max(0, options.RunBudgetBufferMinutes));
        var floor = TimeSpan.FromTicks(Math.Max(MinBudget.Ticks, effectiveFrequency.Ticks / 2));
        var budget = effectiveFrequency - buffer;
        if (budget < floor) budget = floor;
        return budget > effectiveFrequency ? effectiveFrequency : budget;
    }

    public static TimeSpan For(int value, string unit, SyncScheduleOptions options) =>
        For(EffectiveFrequency(value, unit, options), options);

    /// <summary>Phần chờ thêm sau ngân sách trước khi watchdog coi lượt RUNNING là kẹt (pod chết giữa chừng).</summary>
    public static readonly TimeSpan WatchdogGrace = TimeSpan.FromMinutes(15);

    /// <summary>Ngưỡng lượt thủ công / dòng không gắn cấu hình lịch (không có tần suất để tính).</summary>
    public static readonly TimeSpan ManualStaleAfter = TimeSpan.FromMinutes(60);

    /// <summary>Sau bao lâu 1 dòng RUNNING bị coi là kẹt: ngân sách + <see cref="WatchdogGrace"/> (= tần suất + 5 phút khi đệm 10).
    /// Lượt thủ công hoặc dòng không có tần suất dùng <see cref="ManualStaleAfter"/>.</summary>
    public static TimeSpan StaleAfter(string syncType, int? frequencyValue, string? frequencyUnit, SyncScheduleOptions options)
    {
        if (string.Equals(syncType, "MANUAL", StringComparison.OrdinalIgnoreCase) || frequencyValue is not > 0 || string.IsNullOrEmpty(frequencyUnit))
            return ManualStaleAfter;
        return For(frequencyValue.Value, frequencyUnit, options) + WatchdogGrace;
    }
}

/// <summary>Đồng hồ ngân sách của 1 lượt chạy — tạo ngay sau khi giành được khoá, hỏi <see cref="Exceeded"/> trước mỗi
/// lần gọi PMIS / mỗi trang. Dừng mềm: không ngắt giữa chừng 1 lần lưu.</summary>
public sealed class RunBudgetClock
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();

    public RunBudgetClock(TimeSpan budget) => Budget = budget;

    public TimeSpan Budget { get; }
    public TimeSpan Elapsed => _watch.Elapsed;
    public bool Exceeded => _watch.Elapsed >= Budget;

    /// <summary>true nếu đã dùng ≥ <paramref name="fraction"/> (0..1) ngân sách — để chia thời gian giữa các pha của 1 lượt.</summary>
    public bool ExceededFraction(double fraction) => _watch.Elapsed >= TimeSpan.FromTicks((long)(Budget.Ticks * Math.Clamp(fraction, 0, 1)));
}
