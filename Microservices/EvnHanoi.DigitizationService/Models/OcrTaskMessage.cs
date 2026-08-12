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
        /// Phạm vi trang cần bóc tách (xem <see cref="ExtractionScopes"/>). Rỗng = bóc tách mọi
        /// trang, giữ nguyên hành vi cũ. Bước OCR không bị ảnh hưởng, luôn chạy đủ trang.
        /// </summary>
        public string ExtractionScope { get; set; } = ExtractionScopes.Default;
    }
}
