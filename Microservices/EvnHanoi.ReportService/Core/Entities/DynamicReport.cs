// Microservices/EvnHanoi.ReportService/Core/Entities/DynamicReport.cs
using System;

namespace EvnHanoi.ReportService.Core.Entities
{
    public class DynamicReport
    {
        public long Id { get; set; }
        public long GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SqlQuery { get; set; } = string.Empty;
        public string? ParametersJson { get; set; }
        public string? AllowedRoles { get; set; }
        public int IsActive { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
