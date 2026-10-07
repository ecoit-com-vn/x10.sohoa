namespace EvnHanoi.SyncService.Services;

/// <summary>Cấu hình đồng bộ danh sách tài liệu — section "Pmis:DocumentSync" (biến môi trường Pmis__DocumentSync__*).</summary>
public class PmisDocumentSyncOptions
{
    public const string SectionName = "Pmis:DocumentSync";

    /// <summary>Đếm lại tổng tài liệu của 1 owner sau tối thiểu bao nhiêu giờ kể từ lần đếm trước (mặc định 12).</summary>
    public int CountRecheckHours { get; set; } = 12;

    /// <summary>Khi tổng PMIS tăng, lấy phần mới từ (mốc lấy gần nhất − biên) — bù tài liệu ghi lùi ngày (mặc định 7 ngày).</summary>
    public int DeltaSafetyMarginDays { get; set; } = 7;

    /// <summary>Quét đầy đủ owner còn thiếu tài liệu (đã nhận &lt; tổng) tối đa 1 lần / số ngày này (mặc định 7).</summary>
    public int FullRescanIntervalDays { get; set; } = 7;

    /// <summary>Số owner liên tiếp lỗi gọi PMIS thì dừng cả lượt (PMIS đang sập) — mặc định 15.</summary>
    public int MaxConsecutiveOwnerErrors { get; set; } = 15;

    /// <summary>Owner có tổng &gt; ngưỡng này được chia theo khoảng ngày (mỗi khoảng ≤ ngưỡng) — khớp giới hạn an toàn 50.000/lượt.</summary>
    public int WindowThreshold { get; set; } = 50_000;

    /// <summary>Năm bắt đầu khi chia khoảng ngày cho owner rất lớn.</summary>
    public int WindowStartYear { get; set; } = 2000;
}

/// <summary>Trạng thái đồng bộ tài liệu của 1 owner (hoặc 1 khoảng ngày) — ánh xạ PMIS_SYNC_STATE (OBJECT_TYPE='DOC_OWNER'/'DOC_WINDOW').</summary>
public class DocumentOwnerState
{
    public string PmisCode { get; set; } = string.Empty;
    public int? RemoteTotal { get; set; }
    public int? LocalCount { get; set; }
    public DateTime? LastFetchAt { get; set; }
    public DateTime? LastCountAt { get; set; }
    public DateTime? LastFullAt { get; set; }
    public int? ScanSkip { get; set; }
}

public enum DocumentSyncAction
{
    /// <summary>Không có gì mới — không kéo dữ liệu.</summary>
    Skip,

    /// <summary>Chưa có trạng thái nhưng DB đã đủ tài liệu của owner → chỉ ghi trạng thái, không kéo.</summary>
    Seed,

    /// <summary>Kéo phần mới (tuNgay = mốc lấy gần nhất − biên an toàn).</summary>
    Delta,

    /// <summary>Quét đầy đủ (theo trang nhỏ, ngân sách thời gian, tiếp tục theo ScanSkip).</summary>
    Full,

    /// <summary>Tổng quá lớn — chia theo khoảng ngày rồi quét từng khoảng.</summary>
    Windowed
}

public sealed record DocumentSyncPlan(DocumentSyncAction Action, DateTime? TuNgay, string Reason);

/// <summary>Quyết định cách đồng bộ tài liệu của 1 owner — logic thuần (không I/O) để kiểm thử.</summary>
public static class DocumentOwnerSyncPlanner
{
    /// <summary>Giá trị DOC_SCAN_SKIP đánh dấu owner đang quét theo khoảng ngày dở.</summary>
    public const int WindowedMarker = -1;

    /// <summary>true nếu owner tới hạn đếm lại tổng.</summary>
    public static bool IsCountDue(DocumentOwnerState? state, DateTime nowUtc, PmisDocumentSyncOptions opt) =>
        state?.LastCountAt == null || nowUtc - state.LastCountAt.Value >= TimeSpan.FromHours(opt.CountRecheckHours);

    /// <param name="state">Trạng thái đã lưu (null = chưa từng).</param>
    /// <param name="remoteTotal">Tổng PMIS vừa đếm.</param>
    /// <param name="dbCount">Số tài liệu của owner đã có trong DB (cho bootstrap); null = không biết.</param>
    public static DocumentSyncPlan Decide(DocumentOwnerState? state, int remoteTotal, int? dbCount, DateTime nowUtc, PmisDocumentSyncOptions opt)
    {
        var tooBig = remoteTotal > opt.WindowThreshold;

        // 1. Chưa từng đồng bộ theo cơ chế mới.
        if (state?.RemoteTotal == null)
        {
            if (remoteTotal == 0) return new(DocumentSyncAction.Seed, null, "PMIS không có tài liệu");
            if (dbCount is { } have && have >= remoteTotal)
                return new(DocumentSyncAction.Seed, null, $"DB đã có {have}/{remoteTotal} tài liệu — chỉ ghi trạng thái");
            return tooBig
                ? new(DocumentSyncAction.Windowed, null, $"{remoteTotal} tài liệu > {opt.WindowThreshold}: chia theo khoảng ngày")
                : new(DocumentSyncAction.Full, null, $"chưa từng quét, DB có {dbCount?.ToString() ?? "?"}/{remoteTotal}");
        }

        // 2. Đang quét dở: tiếp tục — ưu tiên hơn mọi quyết định khác. ScanSkip = WindowedMarker: đang quét theo khoảng ngày
        // (từng khoảng có trạng thái riêng); > 0: quét đầy đủ dở tại vị trí skip đó.
        if (state.ScanSkip is > 0 or WindowedMarker)
            return tooBig || state.ScanSkip == WindowedMarker
                ? new(DocumentSyncAction.Windowed, null, "tiếp tục quét theo khoảng ngày dở")
                : new(DocumentSyncAction.Full, null, $"tiếp tục quét dở từ skip={state.ScanSkip}");

        var received = state.LocalCount ?? 0;

        // 3. Tổng tăng → chỉ kéo phần mới.
        if (remoteTotal > state.RemoteTotal.Value)
        {
            var from = (state.LastFetchAt ?? state.LastFullAt ?? nowUtc.AddDays(-opt.DeltaSafetyMarginDays * 2))
                .AddDays(-opt.DeltaSafetyMarginDays);
            return new(DocumentSyncAction.Delta, from.Date, $"tổng tăng {state.RemoteTotal}→{remoteTotal}");
        }

        // 4. Tổng không tăng nhưng đã nhận ít hơn tổng → quét đầy đủ, tối đa 1 lần / FullRescanIntervalDays.
        if (received < remoteTotal)
        {
            var last = state.LastFullAt;
            if (last == null || nowUtc - last.Value >= TimeSpan.FromDays(opt.FullRescanIntervalDays))
                return tooBig
                    ? new(DocumentSyncAction.Windowed, null, $"đã nhận {received}/{remoteTotal} — quét lại theo khoảng ngày")
                    : new(DocumentSyncAction.Full, null, $"đã nhận {received}/{remoteTotal} — quét lại");
        }

        return new(DocumentSyncAction.Skip, null, remoteTotal < state.RemoteTotal.Value ? $"tổng giảm {state.RemoteTotal}→{remoteTotal}" : "không đổi");
    }

    /// <summary>Tên khoá trạng thái của 1 khoảng ngày: owner|yyyy hoặc owner|yyyyMM.</summary>
    public static string WindowKey(string owner, int year, int? month = null) =>
        month == null ? $"{owner}|{year:D4}" : $"{owner}|{year:D4}{month:D2}";

    /// <summary>Khoảng [từ, đến] (đến = cuối ngày cuối cùng) của cả năm hoặc 1 tháng.</summary>
    public static (DateTime From, DateTime To) WindowRange(int year, int? month = null)
    {
        if (month == null) return (new DateTime(year, 1, 1), new DateTime(year, 12, 31, 23, 59, 59));
        var first = new DateTime(year, month.Value, 1);
        return (first, first.AddMonths(1).AddSeconds(-1));
    }
}
