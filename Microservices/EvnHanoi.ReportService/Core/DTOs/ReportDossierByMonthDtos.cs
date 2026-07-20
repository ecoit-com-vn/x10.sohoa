namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByMonthFilterDto
    {
        public long? UnitId { get; set; }
        public int? ObjectType { get; set; }
        public int? Year { get; set; }
        public int? Month { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DossierByMonthChartStatDto
    {
        public string GroupName { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public long DocumentCount { get; set; }
        public long PageCount { get; set; }
    }

    public class DossierByMonthRatioStatDto
    {
        public string GroupName { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class ReportMonthLookupDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
