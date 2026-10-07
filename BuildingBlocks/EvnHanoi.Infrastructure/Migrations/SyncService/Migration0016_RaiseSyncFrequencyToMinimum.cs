using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.SyncService;

/// <summary>
/// Nâng tần suất đồng bộ tự động (SYNC_CONFIG) thấp hơn 2 giờ lên đúng 2 giờ — mức tối thiểu mới
/// (Pmis:Schedule:MinFrequencyMinutes = 120). Thời gian chạy tối đa của 1 lượt = tần suất − 10 phút nên tần suất thấp
/// không đủ thời gian đồng bộ khối lượng PMIS thật. Dữ liệu cũ vốn vẫn chạy với mức tối thiểu ở runtime (SyncRunBudget.
/// EffectiveFrequency) — migration này chỉ để màn Thiết lập lịch hiển thị đúng giá trị đang áp dụng. Idempotent
/// (chạy lại không còn dòng nào &lt; 2 giờ). Tăng ROW_VERSION để trang đang mở bị buộc tải lại.
/// </summary>
public class Migration0016_RaiseSyncFrequencyToMinimum : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = @"
            UPDATE SYNC_CONFIG
            SET FREQUENCY_VALUE = 2, FREQUENCY_UNIT = 'HOUR', ROW_VERSION = ROW_VERSION + 1
            WHERE (CASE FREQUENCY_UNIT WHEN 'MINUTE' THEN FREQUENCY_VALUE
                                       WHEN 'HOUR' THEN FREQUENCY_VALUE * 60
                                       WHEN 'DAY' THEN FREQUENCY_VALUE * 1440
                                       ELSE FREQUENCY_VALUE END) < 120";
        command.ExecuteNonQuery();
        return string.Empty;
    }
}
