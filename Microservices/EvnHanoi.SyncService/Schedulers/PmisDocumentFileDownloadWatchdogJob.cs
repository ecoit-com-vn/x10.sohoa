using EvnHanoi.SyncService.Clients;
using Quartz;
using Serilog;

namespace EvnHanoi.SyncService.Schedulers;

/// <summary>
/// Cảnh báo khi PmisDocumentFileDownloadJob ngừng tiến triển mà không ai biết. Phát hiện thật
/// 2026-10-01: job đó đã "biến mất" khỏi production suốt 10 ngày (image deploy cũ hơn thời điểm job được
/// thêm vào codebase — Quartz "Adding N jobs" lúc khởi động thiếu hẳn tên job này) — job không hề crash,
/// không ném exception, không ghi log nào cả, nên không ai phát hiện cho tới khi tự tra DB tay
/// (PMIS_DOCUMENT.FILE_STATUS vẫn PENDING hàng loạt, FILE_ATTEMPTS=0 dù đã tạo nhiều ngày).
///
/// Ngưỡng 4 giờ (so với chu kỳ chạy 1 phút của job chính) đủ rộng để không báo động giả lúc hàng đợi tạm
/// thời rỗng (đã tải hết, chưa có tài liệu mới), nhưng đủ hẹp để phát hiện sớm nếu job lại "biến mất" lần
/// nữa — thay vì phải mất 10 ngày như lần này. Cùng tần suất quét (5 phút) với SyncHistoryWatchdogJob.
///
/// Không tự sửa gì (khác SyncHistoryWatchdogJob — job đó tự đánh FAILED được vì chỉ là update 1 cột trong
/// DB của chính SyncService): nguyên nhân thực tế (như lần này) thường cần deploy lại image, không phải
/// thứ code tự sửa được — chỉ log Warning để người vận hành chủ động kiểm tra.
/// </summary>
public class PmisDocumentFileDownloadWatchdogJob : IJob
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromHours(4);

    private readonly Microsoft.Extensions.Options.IOptions<EvnHanoi.SyncService.Services.PmisDocumentFileOptions> _options;
    private readonly IEquipmentServiceClient _equipmentServiceClient;
    private readonly IPmisClient _pmisClient;

    public PmisDocumentFileDownloadWatchdogJob(IEquipmentServiceClient equipmentServiceClient, IPmisClient pmisClient, Microsoft.Extensions.Options.IOptions<EvnHanoi.SyncService.Services.PmisDocumentFileOptions> options)
    {
        _options = options;
        _equipmentServiceClient = equipmentServiceClient;
        _pmisClient = pmisClient;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            // API DOCUMENT_FILE_DOWNLOAD chưa cấu hình/đang tắt (admin chủ động hoặc mới deploy, chưa nhập URL): job tải file tự dừng
            // có chủ đích — không phải "job biến mất", nên không cảnh báo (tránh ồn log mỗi 5 phút).
            if (!await _pmisClient.IsDocumentFileEndpointActiveAsync()) return;

            var summary = await _equipmentServiceClient.GetPendingDocumentSummaryAsync(_options.Value.ExcludedPrefixList);
            if (summary.PendingCount == 0) return; // hàng đợi rỗng — không có gì để cảnh báo.

            var staleSince = summary.LastDownloadedAt == null
                ? (TimeSpan?)null // chưa từng tải được file nào — luôn coi là "quá hạn" nếu có hàng đợi.
                : DateTime.UtcNow - summary.LastDownloadedAt.Value;

            if (staleSince == null || staleSince.Value > StaleThreshold)
            {
                Log.Warning(
                    "PmisDocumentFileDownloadWatchdogJob: đã {Gio} không tải được file tài liệu PMIS nào dù còn {SoLuong} tài liệu đang chờ — kiểm tra job PmisDocumentFileDownloadJob có đang được Quartz đăng ký không (xem log \"Adding N jobs\" lúc khởi động SyncService) và log của chính job đó.",
                    summary.LastDownloadedAt == null ? "chưa từng" : $"{staleSince!.Value.TotalHours:0} giờ",
                    summary.PendingCount);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PmisDocumentFileDownloadWatchdogJob: lỗi khi kiểm tra hàng đợi tải file tài liệu PMIS.");
        }
    }
}
