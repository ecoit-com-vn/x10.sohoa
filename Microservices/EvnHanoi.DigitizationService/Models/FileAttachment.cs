using System;

namespace EvnHanoi.DigitizationService.Models
{
    public class FileAttachment
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedBy { get; set; }
        public string Status { get; set; } // e.g. "Uploaded", "Processing", "Done"
    }
}
