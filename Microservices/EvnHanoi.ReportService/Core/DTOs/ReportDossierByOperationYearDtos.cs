namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByOperationYearFilterDto
    {
        public long? UnitId { get; set; }

        /// <summary>0 = Tất cả; 1 = Trạm (gộp DOSSIER_GROUP_ID 1+3); 2 = Đường dây (gộp DOSSIER_GROUP_ID 2+4).</summary>
        public int? ObjectType { get; set; }

        /// <summary>Năm vận hành của trạm/đường dây (INFRASTRUCTURE.OPERATION_DATE). Null/&lt;=0 = tất cả các năm.</summary>
        public int? Year { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// 3 box KPI — tổng theo bộ lọc (khớp lưới); % tăng trưởng so với năm vận hành trước đó (Year - 1).
    /// </summary>
    public class DossierByOperationYearSummaryStatsDto
    {
        /// <summary>0 = tất cả các năm.</summary>
        public int Year { get; set; }
        public int PreviousYear { get; set; }
        public bool ShowGrowth { get; set; }
        public long DossierCount { get; set; }
        public decimal? DossierGrowthPercent { get; set; }
        public long DocumentCount { get; set; }
        public decimal? DocumentGrowthPercent { get; set; }
        public long PageCount { get; set; }
        public decimal? PageGrowthPercent { get; set; }
    }
}
