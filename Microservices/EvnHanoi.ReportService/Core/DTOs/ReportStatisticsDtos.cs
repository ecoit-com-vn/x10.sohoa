using System.Collections.Generic;

namespace EvnHanoi.ReportService.Core.DTOs
{
    /// <summary>Dòng danh sách hồ sơ — dùng chung tab List các báo cáo thống kê.</summary>
    public class ReportStatisticsDossierListItemDto
    {
        public int Stt { get; set; }
        public string DossierId { get; set; } = string.Empty;
        public string DossierCode { get; set; } = string.Empty;
        public string DossierTitle { get; set; } = string.Empty;
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

    /// <summary>Dòng danh sách tài liệu — tab Danh sách tài liệu báo cáo theo loại văn bản.</summary>
    public class ReportStatisticsDocumentListItemDto
    {
        public int Stt { get; set; }
        public string DocumentId { get; set; } = string.Empty;
        public string DossierId { get; set; } = string.Empty;
        public string? DocumentTypeId { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        public string DossierTypeName { get; set; } = string.Empty;
        public string InfrastructureName { get; set; } = string.Empty;
        public string EquipmentName { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        /// <summary>Id phiên bản (version) mới nhất của tài liệu — dùng để mở popup xem chi tiết tài liệu.</summary>
        public string? VersionId { get; set; }
        public string? MimeType { get; set; }
    }

    public class ReportStatisticsDocumentListResponseDto
    {
        public List<ReportStatisticsDocumentListItemDto> Items { get; set; } = new();
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

    /// <summary>Lưới thống kê theo loại hồ sơ — mỗi dòng gom theo loại hồ sơ.</summary>
    public class ReportStatisticsDossierTypeGridItemDto
    {
        public int Stt { get; set; }
        public string DossierTypeCode { get; set; } = string.Empty;
        public string DossierTypeName { get; set; } = string.Empty;
        public long TotalDossiers { get; set; }
        public long TotalDocuments { get; set; }
        public long TotalPages { get; set; }
    }

    public class ReportStatisticsDossierTypeGridResponseDto
    {
        public List<ReportStatisticsDossierTypeGridItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>Lưới thống kê theo loại văn bản — mỗi dòng gom theo loại văn bản.</summary>
    public class ReportStatisticsDocumentTypeGridItemDto
    {
        public int Stt { get; set; }
        public string DocumentTypeCode { get; set; } = string.Empty;
        public string DocumentTypeName { get; set; } = string.Empty;
        public long TotalDocuments { get; set; }
        public long TotalPages { get; set; }
    }

    public class ReportStatisticsDocumentTypeGridResponseDto
    {
        public List<ReportStatisticsDocumentTypeGridItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>Lưới thống kê theo kệ lưu trữ — mỗi dòng gom theo kệ hồ sơ.</summary>
    public class ReportStatisticsShelfGridItemDto
    {
        public int Stt { get; set; }
        public string ShelfCode { get; set; } = string.Empty;
        public string ShelfName { get; set; } = string.Empty;
        public long TotalDossiers { get; set; }
        public long TotalDocuments { get; set; }
        public long TotalPages { get; set; }
    }

    public class ReportStatisticsShelfGridResponseDto
    {
        public List<ReportStatisticsShelfGridItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>Lưới thống kê theo hộp lưu trữ — mỗi dòng gom theo hộp hồ sơ.</summary>
    public class ReportStatisticsBoxGridItemDto
    {
        public int Stt { get; set; }
        public string BoxCode { get; set; } = string.Empty;
        public string BoxName { get; set; } = string.Empty;
        public long TotalDossiers { get; set; }
        public long TotalDocuments { get; set; }
        public long TotalPages { get; set; }
    }

    public class ReportStatisticsBoxGridResponseDto
    {
        public List<ReportStatisticsBoxGridItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>Lưới thống kê theo tầng lưu trữ — mỗi dòng gom theo tầng hồ sơ.</summary>
    public class ReportStatisticsFloorGridItemDto
    {
        public int Stt { get; set; }
        public string FloorCode { get; set; } = string.Empty;
        public string FloorName { get; set; } = string.Empty;
        public long TotalDossiers { get; set; }
        public long TotalDocuments { get; set; }
        public long TotalPages { get; set; }
    }

    public class ReportStatisticsFloorGridResponseDto
    {
        public List<ReportStatisticsFloorGridItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>Lưới thống kê theo người tạo hồ sơ — báo cáo phân bổ.</summary>
    public class ReportStatisticsCreatorGridItemDto
    {
        public int Stt { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public long TotalDossiers { get; set; }
        public long TotalDocuments { get; set; }
        public long TotalPages { get; set; }
    }

    public class ReportStatisticsCreatorGridResponseDto
    {
        public List<ReportStatisticsCreatorGridItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>Lưới hồ sơ theo thiết bị — mỗi dòng là một thiết bị cụ thể (không gom theo loại).</summary>
    public class ReportStatisticsEquipmentGridItemDto
    {
        public int Stt { get; set; }
        public string EquipmentCode { get; set; } = string.Empty;
        public string EquipmentName { get; set; } = string.Empty;
        public string InfrastructureName { get; set; } = string.Empty;
        public int? ManufactureYear { get; set; }
        public long TotalDossiers { get; set; }
        public long TotalDocuments { get; set; }
        public long TotalPages { get; set; }
    }

    public class ReportStatisticsEquipmentGridResponseDto
    {
        public List<ReportStatisticsEquipmentGridItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>Lưới hồ sơ theo thiết bị (theo tình trạng) — mỗi dòng là một thiết bị cụ thể.</summary>
    public class ReportStatisticsEquipmentStatusGridItemDto
    {
        public int Stt { get; set; }
        public string EquipmentCode { get; set; } = string.Empty;
        public string EquipmentName { get; set; } = string.Empty;
        public string InfrastructureName { get; set; } = string.Empty;
        public string EquipmentStatusName { get; set; } = string.Empty;
        public long TotalDossiers { get; set; }
        public long TotalDocuments { get; set; }
        public long TotalPages { get; set; }
    }

    public class ReportStatisticsEquipmentStatusGridResponseDto
    {
        public List<ReportStatisticsEquipmentStatusGridItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>Lưới hồ sơ được tra cứu nhiều nhất — mỗi dòng là một hồ sơ, kèm tổng số lượt tra cứu.</summary>
    public class ReportStatisticsDossierViewGridItemDto
    {
        public int Stt { get; set; }
        public string DossierId { get; set; } = string.Empty;
        public Dictionary<string, string> CatalogData { get; set; } = new();
        public string InfrastructureName { get; set; } = string.Empty;
        public long ViewCount { get; set; }
    }

    public class ReportStatisticsDossierViewGridResponseDto
    {
        public List<ReportStatisticsDossierViewGridItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
