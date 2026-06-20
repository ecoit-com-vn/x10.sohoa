using System.Collections.Generic;

namespace EvnHanoi.DigitizationService.Models
{
    public class ExtractionTaskMessage
    {
        public int FileId { get; set; }
        public string FilePath { get; set; }
        public string BucketName { get; set; }
        
        /// <summary>
        /// Danh sách các form cần bóc tách. Mỗi form tương ứng với một đối tượng được bóc tách từ PDF.
        /// </summary>
        public List<ExtractionForm> Forms { get; set; } = new List<ExtractionForm>();
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
