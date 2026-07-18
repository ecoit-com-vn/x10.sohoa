// Microservices/EvnHanoi.ReportService/Core/Entities/Report.cs
using System;

namespace EvnHanoi.ReportService.Core.Entities
{
    public class Report
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
