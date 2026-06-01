// Microservices/EvnHanoi.ReportService/Core/Entities/ReportGroup.cs
using System;
using System.Collections.Generic;

namespace EvnHanoi.ReportService.Core.Entities
{
    public class ReportGroup
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        
        public List<DynamicReport> DynamicReports { get; set; } = new();
    }
}
