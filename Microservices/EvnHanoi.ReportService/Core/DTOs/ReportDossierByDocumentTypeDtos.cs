namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByDocumentTypeFilterDto
    {
        public long? UnitId { get; set; }
        public List<string>? DocumentTypeIds { get; set; }
        public int? Year { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DossierByDocumentTypeChartStatDto
    {
        public string DocumentTypeCode { get; set; } = string.Empty;
        public string DocumentTypeName { get; set; } = string.Empty;
        public long DocumentCount { get; set; }
        public long PageCount { get; set; }
    }
}
