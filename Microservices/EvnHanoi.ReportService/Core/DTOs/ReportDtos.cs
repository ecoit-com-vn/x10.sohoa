// Microservices/EvnHanoi.ReportService/Core/DTOs/ReportDtos.cs
using System;
using System.Collections.Generic;

namespace EvnHanoi.ReportService.Core.DTOs
{
    public class ReportGroupDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public List<DynamicReportDto> DynamicReports { get; set; } = new();
    }

    public class ReportGroupCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Description { get; set; }
    }

    public class ReportGroupUpdateDto
    {
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Description { get; set; }
    }

    public class DynamicReportDto
    {
        public long Id { get; set; }
        public long GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SqlQuery { get; set; } = string.Empty;
        public string? ParametersJson { get; set; }
        public string? AllowedRoles { get; set; }
        public bool IsActive { get; set; }
    }

    public class DynamicReportCreateDto
    {
        public long GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SqlQuery { get; set; } = string.Empty;
        public string? ParametersJson { get; set; }
        public string? AllowedRoles { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class DynamicReportUpdateDto
    {
        public long GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SqlQuery { get; set; } = string.Empty;
        public string? ParametersJson { get; set; }
        public string? AllowedRoles { get; set; }
        public bool IsActive { get; set; }
    }

    public class ExecuteReportRequest
    {
        public Dictionary<string, object>? Parameters { get; set; }
    }
}
