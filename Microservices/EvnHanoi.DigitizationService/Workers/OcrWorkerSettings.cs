using EvnHanoi.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;

namespace EvnHanoi.DigitizationService.Workers;

/// <summary>Luồng (lane) xử lý OCR: tài liệu nhỏ và tài liệu lớn đi hai hàng đợi riêng, mỗi hàng có số chỗ xử lý song song riêng.</summary>
public sealed record OcrLane(string Name, string QueueName, string RoutingKey, ushort PrefetchCount, bool IsLarge)
{
    /// <summary>Luồng mặc định: nhận mọi tác vụ từ publisher (queue gốc), tự chuyển tài liệu lớn sang <see cref="Large"/>.</summary>
    public static OcrLane Standard(IConfiguration configuration) => new(
        "small", DigitizationTopicTopology.OcrTaskQueue, DigitizationTopicTopology.OcrTaskRoutingKey,
        OcrWorkerSettings.GetPrefetchCount(configuration), IsLarge: false);

    public static OcrLane Large(IConfiguration configuration) => new(
        "large", DigitizationTopicTopology.OcrTaskLargeQueue, DigitizationTopicTopology.OcrTaskLargeRoutingKey,
        OcrWorkerSettings.GetLargePrefetchCount(configuration), IsLarge: true);
}

/// <summary>Cấu hình số tài liệu OCR xử lý song song mỗi instance, tách theo luồng.
/// <list type="bullet">
/// <item><c>Ocr:PrefetchCount</c> (mặc định 2) — số tài liệu NHỎ chạy cùng lúc.</item>
/// <item><c>Ocr:LargePrefetchCount</c> (mặc định 1) — số tài liệu LỚN chạy cùng lúc.</item>
/// <item><c>Ocr:LargeLaneEnabled</c> (mặc định true) — false: tắt tách luồng, luồng lớn xả message về queue chính.</item>
/// <item><c>Ocr:LargeJobPages</c> (mặc định 30) / <c>Ocr:LargeJobSizeMB</c> (mặc định 20) — ngưỡng coi là tài liệu lớn (đạt MỘT trong hai); 0 = tắt tiêu chí đó.</item>
/// </list>
/// Mỗi luồng có channel riêng với prefetch và consumerDispatchConcurrency bằng đúng số chỗ của luồng đó (xem OcrWorker.ExecuteAsync).
/// Lưu ý: tách luồng cải thiện độ trễ của tài liệu nhỏ, KHÔNG tăng công suất GPU. Mỗi trang đã tự bắn song song OCR_VL_PARALLEL crop lên máy chủ OCR nên
/// 1–2 trang đang chạy là đủ làm đầy các slot; thêm tài liệu song song chỉ lấp khoảng trống lúc render/dựng PDF, không nên đặt cao. Đo trên GPU Turing 8GB:
/// quá 2 slot không nhanh thêm — GPU khác (A100 dùng chung với LLM/STT) cần đo lại.</summary>
public static class OcrWorkerSettings
{
    public const int DefaultPrefetchCount = 2;
    public const int DefaultLargePrefetchCount = 1;
    public const int DefaultLargeJobPages = 30;
    public const int DefaultLargeJobSizeMb = 20;

    public static ushort GetPrefetchCount(IConfiguration configuration) =>
        (ushort)Math.Clamp(configuration.GetValue("Ocr:PrefetchCount", DefaultPrefetchCount), 1, 16);

    /// <summary>Mức xử lý đồng thời mặc định của kết nối RabbitMQ — hiện chỉ <c>ExtractionWorker</c> kế thừa (các OcrWorker đặt riêng theo channel).
    /// Cấu hình <c>Extraction:DispatchConcurrency</c> (mặc định 2 = số slot của LLM server), TÁCH khỏi <c>Ocr:PrefetchCount</c> để chỉnh OCR không làm đổi tải lên LLM.</summary>
    public static ushort GetExtractionDispatchConcurrency(IConfiguration configuration) =>
        (ushort)Math.Clamp(configuration.GetValue("Extraction:DispatchConcurrency", 2), 1, 16);

    public static ushort GetLargePrefetchCount(IConfiguration configuration) =>
        (ushort)Math.Clamp(configuration.GetValue("Ocr:LargePrefetchCount", DefaultLargePrefetchCount), 1, 16);

    /// <summary>Công tắc luồng lớn (<c>Ocr:LargeLaneEnabled</c>, mặc định true). Tắt = luồng nhỏ không chuyển file nữa, luồng lớn chỉ "xả" message còn lại về queue chính
    /// (dùng trước khi rollback image — bản cũ không đọc queue lớn — hoặc khi luồng lớn có sự cố).</summary>
    public static bool IsLargeLaneEnabled(IConfiguration configuration) =>
        configuration.GetValue("Ocr:LargeLaneEnabled", true);

    /// <summary>Tài liệu có được coi là "lớn" (đi luồng riêng) không — theo số trang hoặc dung lượng file.</summary>
    public static bool IsLargeJob(IConfiguration configuration, int pageCount, long fileBytes)
    {
        var pages = configuration.GetValue("Ocr:LargeJobPages", DefaultLargeJobPages);
        var sizeMb = configuration.GetValue("Ocr:LargeJobSizeMB", DefaultLargeJobSizeMb);
        return (pages > 0 && pageCount >= pages) || (sizeMb > 0 && fileBytes >= sizeMb * 1024L * 1024L);
    }
}
