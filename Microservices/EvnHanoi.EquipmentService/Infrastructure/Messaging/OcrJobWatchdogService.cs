using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.EquipmentService.Infrastructure.Messaging;

/// <summary>
/// Quét định kỳ các job OCR/bóc tách (DOCUMENT_OCR_PROGRESS) đứng yên quá lâu ở Pending/Running/
/// Extracting mà không có bất kỳ cập nhật tiến độ nào — dấu hiệu worker (OcrWorker/ExtractionWorker)
/// đã treo hoặc tiến trình bị dừng/crash giữa chừng, khiến message RabbitMQ tương ứng biến mất mà
/// không còn cách nào tự phục hồi (xem QLSHX10_DOCS/BAO_CAO_NGUYEN_NHAN_OCR_KET_RUNNING.md — "Nhóm B").
///
/// Đây là lớp bảo vệ độc lập với cơ chế retry/DLQ cấp tài liệu trong OcrWorker/ExtractionWorker
/// (Phase 1.3 của kế hoạch fix) — cơ chế đó chỉ bắt được lỗi khi worker THỰC SỰ ném exception; watchdog
/// này bắt trường hợp còn lại: worker không kịp/không thể ném exception (treo thật, bị kill).
/// </summary>
public class OcrJobWatchdogService : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OcrJobWatchdogService> _logger;

    // Job ĐÃ được worker nhận (có tiến độ) mà đứng yên quá ngưỡng này = nghi worker treo/crash. Cấu hình Ocr:StartedStaleMinutes (mặc định 30).
    private readonly TimeSpan _startedThreshold;
    // Job CHƯA được worker nhận (còn xếp hàng RabbitMQ): chờ lâu là bình thường khi hàng dài — chỉ coi là mất message sau ngưỡng dài này.
    // Cấu hình Ocr:QueuedStaleHours (mặc định 24).
    private readonly TimeSpan _queuedThreshold;

    public OcrJobWatchdogService(IServiceScopeFactory scopeFactory, ILogger<OcrJobWatchdogService> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _startedThreshold = TimeSpan.FromMinutes(Math.Max(5, configuration.GetValue("Ocr:StartedStaleMinutes", 30)));
        _queuedThreshold = TimeSpan.FromHours(Math.Max(1, configuration.GetValue("Ocr:QueuedStaleHours", 24)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OcrJobWatchdogService bắt đầu quét mỗi {IntervalMinutes} phút, ngưỡng treo (đã chạy) {StartedMinutes} phút, ngưỡng chờ hàng đợi {QueuedHours} giờ.",
            ScanInterval.TotalMinutes, _startedThreshold.TotalMinutes, _queuedThreshold.TotalHours);

        using var timer = new PeriodicTimer(ScanInterval);
        do
        {
            try
            {
                await ScanAndMarkStaleJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi quét job OCR/bóc tách bị treo.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ScanAndMarkStaleJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDocumentDigitizationRepository>();
        var digitizationService = scope.ServiceProvider.GetRequiredService<IDocumentDigitizationService>();

        var staleJobs = (await repository.GetStaleJobsAsync(_startedThreshold, _queuedThreshold)).ToList();
        if (staleJobs.Count == 0)
            return;

        foreach (var job in staleJobs)
        {
            var lastActivity = job.ModifiedDate ?? job.CreatedDate;
            var queued = string.Equals(job.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                         || (job.Action?.EndsWith(".process.task", StringComparison.OrdinalIgnoreCase) ?? false);
            var thresholdText = queued ? $"{(int)_queuedThreshold.TotalHours} giờ" : $"{(int)_startedThreshold.TotalMinutes} phút";
            _logger.LogWarning(
                "Job OCR/bóc tách version {VersionId} đứng yên ở '{Status}' ({Kind}) từ {LastActivity} (quá {Threshold}) — đánh dấu Failed.",
                job.DocumentVersionId, job.Status, queued ? "chờ hàng đợi" : "đã chạy", lastActivity, thresholdText);

            // Tái dùng đúng HandleProgressMessageAsync (đã xử lý action "*.process.failed" ở Phase 1.3)
            // để cập nhật Status=Failed + ErrorMessage + push SignalR — không lặp lại logic.
            var action = job.Phase == "extraction" ? "extraction.process.failed" : "ocr.process.failed";
            await digitizationService.HandleProgressMessageAsync(new DigitizationProgressMessage
            {
                FileId = job.DocumentVersionId,
                Action = action,
                ErrorMessage = queued
                    ? $"Job chờ trong hàng đợi quá {thresholdText} chưa được xử lý — có thể message đã mất, hãy chạy lại."
                    : $"Job vượt quá thời gian xử lý cho phép (>{thresholdText} không cập nhật) " +
                      "— có khả năng worker bị treo hoặc mất kết nối."
            });
        }

        _logger.LogInformation("Watchdog đã đánh dấu Failed {Count} job OCR/bóc tách bị treo.", staleJobs.Count);
    }
}
