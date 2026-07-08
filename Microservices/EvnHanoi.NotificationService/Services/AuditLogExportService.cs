using ClosedXML.Excel;
using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Services;

public interface IAuditLogExportService
{
    byte[] BuildExcel(IReadOnlyList<AuditLogItemDto> logs);
}

public sealed class AuditLogExportService : IAuditLogExportService
{
    public byte[] BuildExcel(IReadOnlyList<AuditLogItemDto> logs)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("NhatKyHeThong");

        var headers = new[]
        {
            "Mã nhật ký", "Thời gian", "Người dùng", "Hành động", "Service",
            "Loại đối tượng", "Mã đối tượng", "Tên đối tượng", "Chi tiết", "HTTP", "Mã trạng thái"
        };

        for (var col = 0; col < headers.Length; col++)
            sheet.Cell(1, col + 1).Value = headers[col];

        var headerRange = sheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        var row = 2;
        foreach (var log in logs)
        {
            sheet.Cell(row, 1).Value = log.Id;
            sheet.Cell(row, 2).Value = log.Timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
            sheet.Cell(row, 3).Value = log.UserName;
            sheet.Cell(row, 4).Value = log.Action;
            sheet.Cell(row, 5).Value = log.ServiceName ?? string.Empty;
            sheet.Cell(row, 6).Value = log.ResourceType ?? string.Empty;
            sheet.Cell(row, 7).Value = log.ResourceId ?? string.Empty;
            sheet.Cell(row, 8).Value = log.ResourceName ?? string.Empty;
            sheet.Cell(row, 9).Value = log.Details ?? string.Empty;
            sheet.Cell(row, 10).Value = log.HttpMethod ?? string.Empty;
            sheet.Cell(row, 11).Value = log.StatusCode?.ToString() ?? string.Empty;
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
