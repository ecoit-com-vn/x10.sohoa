using System.Collections.Generic;

namespace EvnHanoi.DigitizationService.Models
{
    public class ExtractionTaskMessage
    {
        public Guid FileId { get; set; }
        public string FilePath { get; set; }
        public string BucketName { get; set; }
        public string ExtractPrompt { get; set; }
        public Guid? EquipmentId { get; set; }

        /// <summary>Danh sách các form cần bóc tách. Mỗi form tương ứng với một đối tượng được bóc tách từ PDF.
        /// </summary>
        public ExtractionForm Form { get; set; }

        /// <summary>Snapshot đầy đủ EAV FormSchema JSON — tham chiếu khi bóc tách.</summary>
        public string FormSchemaJson { get; set; }
    }

    public class ExtractionForm
    {
        public string FormId { get; set; }
        public string FormName { get; set; }
        
        /// <summary>
        /// Danh sách các trường cần bóc tách trong form này.
        /// </summary>
        public List<ExtractionFormField> Fields { get; set; } = new List<ExtractionFormField>();
    }

    public class ExtractionFormField
    {
        public string FieldName { get; set; }
        public string Description { get; set; }
    }
}
