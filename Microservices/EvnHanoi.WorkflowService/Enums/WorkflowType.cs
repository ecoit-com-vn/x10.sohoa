using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace EvnHanoi.WorkflowService.Enums
{
    public enum WorkflowType
    {
        [Description("Quy trình số hóa hồ sơ")]
        DigitizationWireDossier = 1,

        [Description("Quy trình mượn/trả hồ sơ kỹ thuật")]
        BorrowReturnTechnicalDossier = 2,
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
