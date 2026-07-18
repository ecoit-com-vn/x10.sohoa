// Microservices/EvnHanoi.ReportService/Core/Entities/ReportGroup.cs
using System;
using System.Collections.Generic;

namespace EvnHanoi.ReportService.Core.Entities
{
    public class ReportGroup
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public int IsDeleted { get; set; }
        public int IsActive { get; set; } = 1;
        
        // Cột ảo phục vụ API list gọn nhẹ
        public int ReportCount { get; set; }
        public int UnitCount { get; set; }
        
        public List<Report> Reports { get; set; } = new();
        public List<long> UnitIds { get; set; } = new();
    }
}
