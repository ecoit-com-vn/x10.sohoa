// Microservices/EvnHanoi.ReportService/Core/Entities/ReportUnitPublish.cs
using System;
using System.Collections.Generic;

namespace EvnHanoi.ReportService.Core.Entities
{
    public class ReportUnitPublish
    {
        public long Id { get; set; }
        public long ReportId { get; set; }
        public long UnitId { get; set; }
        public int IsPublish { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        // Cột hiển thị phục vụ màn lưới danh sách (join với REPORTS)
        public string ReportCode { get; set; } = string.Empty;
        public string ReportName { get; set; } = string.Empty;

        public List<long> RoleIds { get; set; } = new();
    }
}
