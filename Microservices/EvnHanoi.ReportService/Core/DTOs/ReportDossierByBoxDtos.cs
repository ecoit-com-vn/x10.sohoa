namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByBoxFilterDto
    {
        public long? UnitId { get; set; }
        public List<string>? BoxIds { get; set; }
        public int? Year { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DossierByBoxChartStatDto
    {
        public string BoxCode { get; set; } = string.Empty;
        public string BoxName { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public long DocumentCount { get; set; }
        public long PageCount { get; set; }
    }
}
