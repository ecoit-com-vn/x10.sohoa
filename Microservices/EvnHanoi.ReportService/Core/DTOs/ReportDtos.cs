// Microservices/EvnHanoi.ReportService/Core/DTOs/ReportDtos.cs
using System;
using System.Collections.Generic;

namespace EvnHanoi.ReportService.Core.DTOs
{
    public class ReportDto
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class ReportGroupDto
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int ReportCount { get; set; }
        public int UnitCount { get; set; }
        public List<long> ReportIds { get; set; } = new();
        public List<long> UnitIds { get; set; } = new();
        public List<ReportDto> Reports { get; set; } = new();
    }

    public class ReportGroupCreateDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public List<long> ReportIds { get; set; } = new();
        public List<long> UnitIds { get; set; } = new();
    }

    public class ReportGroupUpdateDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public List<long> ReportIds { get; set; } = new();
        public List<long> UnitIds { get; set; } = new();
    }
}
