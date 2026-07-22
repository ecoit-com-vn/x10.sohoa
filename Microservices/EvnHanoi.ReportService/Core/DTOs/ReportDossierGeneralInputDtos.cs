namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierGeneralInputFilterDto
    {
        public long? UnitId { get; set; }

        /// <summary>0: Tất cả, 1: Trạm biến áp, 2: Đường dây, 3: Thiết bị (gộp 3 &amp; 4).</summary>
        public int? ObjectType { get; set; }

        /// <summary>Lọc hồ sơ có d.CreatedDate &gt;= FromDate.</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Lọc hồ sơ có d.CreatedDate &lt;= ToDate.</summary>
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DossierGeneralInputChartStatDto
    {
        public string GroupName { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public long DocumentCount { get; set; }
        public long PageCount { get; set; }
    }

    public class DossierGeneralInputRatioStatDto
    {
        public string GroupName { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public decimal Percentage { get; set; }
    }
}
