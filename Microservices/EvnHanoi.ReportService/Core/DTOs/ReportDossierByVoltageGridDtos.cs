namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByVoltageGridFilterDto
    {
        public long? UnitId { get; set; }
        public int? ObjectType { get; set; }
        public int? GridTypeId { get; set; }
        public int? Year { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DossierByVoltageGridChartStatDto : DossierByMonthChartStatDto
    {
    }

    public class DossierByVoltageGridRatioStatDto
    {
        public string GroupName { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public decimal Percentage { get; set; }
    }
}
