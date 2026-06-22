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
        public ExtractionForm Form { get; set; }
    }
}
