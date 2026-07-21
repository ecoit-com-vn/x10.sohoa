namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByDossierTypeFilterDto
    {
        public long? UnitId { get; set; }
        public List<string>? DossierTypeIds { get; set; }
        public int? Year { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DossierByDossierTypeChartStatDto
    {
        public string DossierTypeCode { get; set; } = string.Empty;
        public string DossierTypeName { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public long DocumentCount { get; set; }
        public long PageCount { get; set; }
    }
}
