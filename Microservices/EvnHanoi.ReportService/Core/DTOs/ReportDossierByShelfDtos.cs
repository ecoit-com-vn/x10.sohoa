namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByShelfFilterDto
    {
        public long? UnitId { get; set; }
        public List<string>? ShelfIds { get; set; }
        public int? Year { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DossierByShelfChartStatDto
    {
        public string ShelfCode { get; set; } = string.Empty;
        public string ShelfName { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public long DocumentCount { get; set; }
        public long PageCount { get; set; }
    }
}
