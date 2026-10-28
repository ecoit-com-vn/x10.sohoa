using System.Collections.Generic;

namespace EvnHanoi.DigitizationService.Models
{
    public class OcrTaskMessage
    {
        public Guid FileId { get; set; }
        public string FilePath { get; set; }
        public string BucketName { get; set; }
        public string Action { get; set; } = "ocr.process.task";
        public string ProcessOption { get; set; } = "OcrAndExtract"; // "OcrAndExtract" or "ExtractOnly"
        public string ExtractPrompt { get; set; }
        public ExtractionForm Form { get; set; }
        /// <summary>Snapshot đầy đủ EAV FormSchema JSON — tham chiếu khi bóc tách.</summary>
        public string FormSchemaJson { get; set; }
        /// <summary>Thiết bị kỹ thuật — dùng suffix _eq_{EquipmentId} khi lưu kết quả bóc tách.</summary>
        public Guid? EquipmentId { get; set; }

        /// <summary>
        /// Bắt buộc bóc tách lại từ LLM, bỏ qua ResultCache.
        /// </summary>
        public bool ForceReExtract { get; set; } = false;

        /// <summary>
        /// Phạm vi trang cần bóc tách (xem <see cref="ExtractionScopes"/>). Rỗng = bóc tách mọi
        /// trang, giữ nguyên hành vi cũ. Bước OCR không bị ảnh hưởng, luôn chạy đủ trang.
        /// </summary>
        public string ExtractionScope { get; set; } = ExtractionScopes.Default;

        /// <summary>
        /// Chỉ set cho luồng "Quản lý dữ liệu huấn luyện AI-OCR" (upload PDF độc lập, không gắn
        /// Dossier/Equipment) — khi có giá trị, OcrWorker sau khi OCR xong sẽ nạp thẳng kết quả vào
        /// OCR_MODULE_REGION của Job này thay vì publish extraction.process.task. Luồng dossier/equipment
        /// hiện tại không set field này nên hành vi giữ nguyên không đổi.
        /// </summary>
        public string? OcrModuleJobId { get; set; }

        /// <summary>Gợi ý số trang / dung lượng do publisher gửi kèm (có thể thiếu hoặc lệch) — dùng chọn luồng OCR nhỏ/lớn trước khi tải file.</summary>
        public int? PageCount { get; set; }
        public long? FileSizeBytes { get; set; }
    }
}
