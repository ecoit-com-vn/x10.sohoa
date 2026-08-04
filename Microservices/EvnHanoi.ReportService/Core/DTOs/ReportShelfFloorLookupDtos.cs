namespace EvnHanoi.ReportService.Core.DTOs;

public class ReportShelfFloorLookupDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ReportFloorLookupDto> Floors { get; set; } = new();
}

public class ReportFloorLookupDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShelfId { get; set; } = string.Empty;
}
