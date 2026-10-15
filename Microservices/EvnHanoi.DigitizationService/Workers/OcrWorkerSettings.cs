using Microsoft.Extensions.Configuration;

namespace EvnHanoi.DigitizationService.Workers;

/// <summary>Cấu hình số tài liệu OCR xử lý song song mỗi instance (khoá Ocr:PrefetchCount, mặc định 2). Dùng CHUNG cho prefetch của OcrWorker và
/// ConsumerDispatchConcurrency của kết nối RabbitMQ (Program.cs) để hai giá trị không lệch nhau. Mặc định 2 vì nghẽn thật là GPU của máy chủ OCR —
/// chỉ tăng khi máy chủ còn dư công suất.</summary>
public static class OcrWorkerSettings
{
    public const int DefaultPrefetchCount = 2;

    public static ushort GetPrefetchCount(IConfiguration configuration) =>
        (ushort)Math.Clamp(configuration.GetValue("Ocr:PrefetchCount", DefaultPrefetchCount), 1, 16);
}
