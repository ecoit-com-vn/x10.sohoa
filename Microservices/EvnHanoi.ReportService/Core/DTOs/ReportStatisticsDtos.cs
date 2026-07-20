using System.Collections.Generic;

namespace EvnHanoi.ReportService.Core.DTOs
{
    /// <summary>Dòng danh sách hồ sơ — dùng chung tab List các báo cáo thống kê.</summary>
    public class ReportStatisticsDossierListItemDto
    {
        public int Stt { get; set; }
        public string DossierId { get; set; } = string.Empty;
        public string InfrastructureName { get; set; } = string.Empty;
        public string DossierTypeName { get; set; } = string.Empty;
        public Dictionary<string, string> CatalogData { get; set; } = new();
        public long DocumentCount { get; set; }
    }

    public class ReportStatisticsDossierListResponseDto
    {
        public List<ReportStatisticsDossierListItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>Lưới thống kê theo trạm/đường dây — mỗi dòng là 1 infrastructure, không phải hồ sơ.</summary>
    public class ReportStatisticsStationGridItemDto
    {
        public int Stt { get; set; }
        public Dictionary<string, string> CatalogData { get; set; } = new();
        public string GridTypeName { get; set; } = string.Empty;
        public long TotalDossiers { get; set; }
        public long TotalDocuments { get; set; }
        public long TotalPages { get; set; }
    }

    public class ReportStatisticsStationGridResponseDto
    {
        public List<ReportStatisticsStationGridItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>Lưới thống kê theo loại thiết bị — mỗi dòng gom theo loại thiết bị + lưới điện.</summary>
    public class ReportStatisticsEquipmentTypeGridItemDto
    {
        public int Stt { get; set; }
        public string EquipmentTypeCode { get; set; } = string.Empty;
        public string EquipmentTypeName { get; set; } = string.Empty;
        public string GridTypeName { get; set; } = string.Empty;
        public long TotalDossiers { get; set; }
        public long TotalDocuments { get; set; }
        public long TotalPages { get; set; }
    }

    public class ReportStatisticsEquipmentTypeGridResponseDto
    {
        public List<ReportStatisticsEquipmentTypeGridItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
