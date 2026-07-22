namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByOperationTimeFilterDto
    {
        public long? UnitId { get; set; }

        /// <summary>0 = Tất cả; 1 = Trạm (gộp DOSSIER_GROUP_ID 1+3); 2 = Đường dây (gộp DOSSIER_GROUP_ID 2+4).</summary>
        public int? ObjectType { get; set; }

        /// <summary>Lọc hồ sơ của trạm/đường dây có INFRASTRUCTURE.OPERATION_DATE &gt;= FromDate.</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Lọc hồ sơ của trạm/đường dây có INFRASTRUCTURE.OPERATION_DATE &lt;= ToDate.</summary>
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>3 box KPI — tổng theo bộ lọc (khớp lưới); không có % tăng trưởng.</summary>
    public class DossierByOperationTimeSummaryStatsDto
    {
        public long DossierCount { get; set; }
        public long DocumentCount { get; set; }
        public long PageCount { get; set; }
    }
}
