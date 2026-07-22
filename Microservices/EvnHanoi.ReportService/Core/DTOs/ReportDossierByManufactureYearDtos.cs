namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByManufactureYearFilterDto
    {
        public long? UnitId { get; set; }

        /// <summary>Id trạm/đường dây cụ thể (INFRASTRUCTURE, gồm cả 2 loại) — multi-select.</summary>
        public List<string>? StationIds { get; set; }

        /// <summary>Năm sản xuất thiết bị (EQUIPMENTS.MANUFACTURE_YEAR). Null/&lt;=0 = tất cả các năm.</summary>
        public int? ManufactureYear { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>Biểu đồ cột ngang — số lượng Hồ sơ/Tài liệu/Trang theo từng loại thiết bị.</summary>
    public class DossierByManufactureYearChartStatDto
    {
        public string EquipmentTypeCode { get; set; } = string.Empty;
        public string EquipmentTypeName { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public long DocumentCount { get; set; }
        public long PageCount { get; set; }
    }
}
