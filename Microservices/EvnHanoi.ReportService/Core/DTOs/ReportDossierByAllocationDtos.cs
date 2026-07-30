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

        /// <summary>
        /// Cờ nội bộ cho báo cáo theo cán bộ nhập liệu. Không public setter để model binder
        /// không thể làm thay đổi điều kiện nguồn hồ sơ của báo cáo phân bổ.
        /// </summary>
        internal bool IncludeAllDossierKinds { get; set; }
    }

    public class DossierByInputOfficerFilterDto
    {
        public long? UnitId { get; set; }
        public int? ObjectType { get; set; }
        public int? Year { get; set; }
        public string? CreatedBy { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        internal DossierByAllocationFilterDto ToSharedFilter() => new()
        {
            UnitId = UnitId,
            ObjectType = ObjectType,
            Year = Year,
            CreatedBy = CreatedBy,
            Page = Page,
            PageSize = PageSize,
            IncludeAllDossierKinds = true
        };
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
