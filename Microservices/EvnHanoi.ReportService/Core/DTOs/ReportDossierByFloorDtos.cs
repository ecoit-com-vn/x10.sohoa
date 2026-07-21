namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByFloorFilterDto
    {
        public long? UnitId { get; set; }
        public List<string>? FloorIds { get; set; }
        public int? Year { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DossierByFloorChartStatDto
    {
        public string FloorCode { get; set; } = string.Empty;
        public string FloorName { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public long DocumentCount { get; set; }
        public long PageCount { get; set; }
    }
}
