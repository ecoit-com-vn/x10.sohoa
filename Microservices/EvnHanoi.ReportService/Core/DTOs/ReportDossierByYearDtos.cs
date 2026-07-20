using System;
using System.Collections.Generic;

namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByYearFilterDto
    {
        public long? UnitId { get; set; }
        public int? ObjectType { get; set; } // 0: Tất cả, 1: Trạm biến áp, 2: Đường dây, 3: Thiết bị (gộp 3 & 4)
        public int? Year { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DossierByYearChartStatDto
    {
        public string GroupName { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public long DocumentCount { get; set; }
        public long PageCount { get; set; }
    }

    public class DossierByYearRatioStatDto
    {
        public string GroupName { get; set; } = string.Empty;
        public string GroupCode { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class DossierObjectTypeLookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
