namespace EvnHanoi.ReportService.Core.DTOs
{
    public class DossierByEquipmentTypeFilterDto
    {
        public long? UnitId { get; set; }
        public int? ObjectType { get; set; }
        public List<string>? EquipmentTypeIds { get; set; }
        public int? Year { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DossierByEquipmentTypeChartStatDto
    {
        public string EquipmentTypeCode { get; set; } = string.Empty;
        public string EquipmentTypeName { get; set; } = string.Empty;
        public long DossierCount { get; set; }
        public long DocumentCount { get; set; }
        public long PageCount { get; set; }
    }
}
