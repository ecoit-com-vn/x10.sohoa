using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDocumentDigitizationRepository
{
    Task<DocumentOcrProgress?> GetProgressByVersionIdAsync(Guid documentVersionId);
    Task<DocumentOcrProgress?> GetProgressByIdAsync(Guid id);
    Task<Guid> CreateProgressAsync(DocumentOcrProgress progress);
    Task<bool> UpdateProgressAsync(DocumentOcrProgress progress);

    Task<DocumentExtractionResult?> GetExtractionResultByVersionIdAsync(Guid documentVersionId, Guid? equipmentId = null);
    Task<Guid> CreateExtractionResultAsync(DocumentExtractionResult result);
    Task<bool> UpdateExtractionResultAsync(DocumentExtractionResult result);

    /// <summary>Màn hình giám sát job OCR/bóc tách toàn hệ thống — chỉ đọc, không đụng 6 method trên.</summary>
    Task<(IEnumerable<OcrJobListItemDto> items, int totalCount)> GetJobsPagedAsync(OcrJobListFilter filter);

    /// <summary>
    /// Job đang Pending/Running/Extracting nhưng đứng yên quá lâu (ModifiedDate, hoặc CreatedDate nếu chưa từng cập nhật) — dùng cho watchdog phát hiện job
    /// bị treo (worker crash/hang giữa chừng, message đã mất khỏi RabbitMQ). Tách 2 loại: job ĐÃ được worker nhận (có tiến độ) dùng
    /// <paramref name="startedThreshold"/>; job CHƯA được nhận (còn xếp hàng: Status=Pending hoặc Action kết thúc ".process.task", cả OCR lẫn bóc tách)
    /// dùng <paramref name="queuedThreshold"/> dài hơn. Quy tắc "xếp hàng" lặp lại ở OcrJobWatchdogService (chọn thông báo) — sửa cả hai nơi cùng lúc.
    /// </summary>
    Task<IEnumerable<DocumentOcrProgress>> GetStaleJobsAsync(TimeSpan startedThreshold, TimeSpan queuedThreshold);
}
