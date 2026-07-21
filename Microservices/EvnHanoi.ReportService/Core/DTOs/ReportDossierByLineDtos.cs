namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByLineFilterDto
    {
        public long? UnitId { get; set; }
        public List<string>? LineIds { get; set; }
        public int? Year { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// 3 box KPI — tổng theo bộ lọc (khớp lưới); % tăng trưởng chỉ năm hiện tại (so tháng trước).
    /// </summary>
    public class DossierByLineSummaryStatsDto
    {
        /// <summary>0 = tất cả các năm.</summary>
        public int Year { get; set; }
        public int ReferenceMonth { get; set; }
        public int PreviousMonth { get; set; }
        public bool ShowGrowth { get; set; }
        public long DossierCount { get; set; }
        public decimal? DossierGrowthPercent { get; set; }
        public long DocumentCount { get; set; }
        public decimal? DocumentGrowthPercent { get; set; }
        public long PageCount { get; set; }
        public decimal? PageGrowthPercent { get; set; }
    }
}
