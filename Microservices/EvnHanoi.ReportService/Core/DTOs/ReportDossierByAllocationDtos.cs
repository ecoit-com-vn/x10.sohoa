namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByAllocationFilterDto
    {
        public long? UnitId { get; set; }
        public int? ObjectType { get; set; }
        public int? Year { get; set; }
        /// <summary>Username người tạo hồ sơ (DOSSIERS.CreatorUsername).</summary>
        public string? CreatedBy { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DossierByAllocationChartStatDto : DossierByMonthChartStatDto
    {
    }

    public class DossierByAllocationRatioStatDto : DossierByMonthRatioStatDto
    {
    }

    public class ReportInputUserLookupDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
