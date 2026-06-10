using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace EvnHanoi.WorkflowService.Enums
{
    public enum WorkflowType
    {
        [Description("Quy trình số hóa hồ sơ đường dây")]
        DigitizationWireDossier = 1,

        [Description("Quy trình số hóa hồ sơ trạm biến áp")]
        DigitizationSubstationDossier = 2,

        [Description("Quy trình phê duyệt tài liệu số hóa")]
        ApprovalDigitizedDocument = 3,

        [Description("Quy trình kiểm soát chất lượng OCR")]
        OcrQualityControl = 4,

        [Description("Quy trình mượn/trả hồ sơ kỹ thuật")]
        BorrowReturnTechnicalDossier = 5,

        [Description("Quy trình bàn giao hồ sơ kỹ thuật")]
        HandoverTechnicalDossier = 6,

        [Description("Quy trình hiệu đính tài liệu số hóa")]
        CorrectionDigitizedDocument = 7,

        [Description("Quy trình cấp mã hồ sơ số hóa")]
        CodeGenerationDigitizedDossier = 8
    }

    public static class WorkflowTypeExtensions
    {
        public static string GetDescription(this WorkflowType value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field == null) return value.ToString();
            
            var attribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
            return attribute == null ? value.ToString() : attribute.Description;
        }

        public static List<string> GetDescriptions()
        {
            var list = new List<string>();
            foreach (WorkflowType val in Enum.GetValues(typeof(WorkflowType)))
            {
                list.Add(val.GetDescription());
            }
            return list;
        }
    }
}
