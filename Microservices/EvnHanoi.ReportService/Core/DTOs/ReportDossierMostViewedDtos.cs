namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierMostViewedFilterDto
    {
        public long? UnitId { get; set; }

        /// <summary>0: Tất cả, 1: Trạm biến áp, 2: Đường dây, 3: Thiết bị (gộp 3 &amp; 4). Chỉ áp dụng cho lưới hồ sơ, không ảnh hưởng 3 box KPI.</summary>
        public int? ObjectType { get; set; }

        /// <summary>Lọc lượt tra cứu (LOOKUP_VIEW_LOGS.CreatedDate) &gt;= FromDate.</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Lọc lượt tra cứu (LOOKUP_VIEW_LOGS.CreatedDate) &lt;= ToDate.</summary>
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// 3 box KPI — Box1: lượt tra cứu hồ sơ TBA, Box2: hồ sơ đường dây, Box3: tài liệu (qua tìm kiếm fulltext).
    /// % tăng trưởng so với tháng trước — độc lập với filter khoảng ngày (luôn so tháng hiện tại thực tế).
    /// </summary>
    public class DossierMostViewedSummaryStatsDto
    {
        public long StationViewCount { get; set; }
        public decimal? StationGrowthPercent { get; set; }
        public long LineViewCount { get; set; }
        public decimal? LineGrowthPercent { get; set; }
        public long DocumentViewCount { get; set; }
        public decimal? DocumentGrowthPercent { get; set; }
    }
}
