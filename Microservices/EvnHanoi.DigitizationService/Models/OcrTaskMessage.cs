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
    }
}
