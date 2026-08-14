using EvnHanoi.DigitizationService.Helpers;
using EvnHanoi.DigitizationService.Models.OcrModule;
using EvnHanoi.DigitizationService.Services;
using EvnHanoi.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.DigitizationService.Services.OcrModule;

/// <summary>
/// Đọc file JSON kết quả OCR từng trang đã có sẵn trên MinIO (do OcrWorker ghi ra theo quy ước
/// "{filePath không đuôi .pdf}_page_{n}.json" cho MỌI tài liệu, kể cả tài liệu hồ sơ/thiết bị production)
/// và nạp (materialize) thành các dòng OCR_MODULE_REGION — không gọi lại ocr_vl_server.
/// </summary>
public interface IOcrJsonMaterializer
{
    Task<List<OcrModuleRegion>> MaterializeAsync(string jobId, string bucket, string filePath, int totalPages);
}

public class OcrJsonMaterializer : IOcrJsonMaterializer
{
    private readonly IMinioStorageService _minioService;
    private readonly ILogger<OcrJsonMaterializer> _logger;

    public OcrJsonMaterializer(IMinioStorageService minioService, ILogger<OcrJsonMaterializer> logger)
    {
        _minioService = minioService;
        _logger = logger;
    }

    public async Task<List<OcrModuleRegion>> MaterializeAsync(string jobId, string bucket, string filePath, int totalPages)
    {
        var baseFilePath = filePath;
        if (baseFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            baseFilePath = baseFilePath[..^4];
        }

        var regions = new List<OcrModuleRegion>();

        // totalPages có thể chưa biết chính xác từ FE — dò tuần tự tới khi không còn file trang tiếp theo,
        // giới hạn an toàn 500 trang giống quy ước đã dùng ở MinioOcrTextReader (NotificationService).
        var maxPages = totalPages > 0 ? totalPages : 500;

        for (var page = 1; page <= maxPages; page++)
        {
            var jsonFileName = $"{baseFilePath}_page_{page}.json";
            string pageJson;

            try
            {
                using var stream = await _minioService.DownloadFileAsync(bucket, jsonFileName);
                using var reader = new StreamReader(stream);
                pageJson = await reader.ReadToEndAsync();
            }
            catch (Exception)
            {
                if (totalPages > 0)
                {
                    _logger.LogWarning(
                        "Job {JobId}: không tìm thấy file OCR JSON trang {Page} ({FileName}) dù kỳ vọng {TotalPages} trang.",
                        jobId, page, jsonFileName, totalPages);
                    continue;
                }

                // Không biết trước totalPages — coi như đã hết trang khi không tìm thấy file tiếp theo.
                break;
            }

            var boxes = OcrPageContentHelper.DeserializeOcrResponse(pageJson);
            for (var i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                if (box.Box == null || box.Box.Count != 4 || string.IsNullOrWhiteSpace(box.Text))
                    continue;

                // Box trong JSON là pixel tuyệt đối theo ảnh OcrWorker render lúc OCR (OcrSourceDpi),
                // nhưng ảnh hiển thị cho FE (GetPageImage) và mọi region khác đều ở DisplayDpi —
                // quy đổi ngay tại đây để BoxX0..Y1 lưu trong OCR_MODULE_REGION luôn khớp DisplayDpi.
                //
                // SourceIndex = i (vị trí trong "boxes" gốc, TÍNH CẢ các box bị bỏ qua ở trên) — để
                // khi sửa tay nội dung, patchedArray[region.SourceIndex] luôn trúng đúng phần tử đã đọc.
                regions.Add(new OcrModuleRegion
                {
                    Id = UuidHelper.NewUuid(),
                    JobId = jobId,
                    PageNumber = page,
                    BoxX0 = box.Box[0] * OcrModuleImageDpi.SourceToDisplayScale,
                    BoxY0 = box.Box[1] * OcrModuleImageDpi.SourceToDisplayScale,
                    BoxX1 = box.Box[2] * OcrModuleImageDpi.SourceToDisplayScale,
                    BoxY1 = box.Box[3] * OcrModuleImageDpi.SourceToDisplayScale,
                    TextRaw = OcrPageContentHelper.NormalizeUtf8Text(box.Text),
                    Confidence = box.Confidence,
                    RegionType = "Text",
                    Status = "Detected",
                    SourceIndex = i,
                });
            }
        }

        return regions;
    }
}
