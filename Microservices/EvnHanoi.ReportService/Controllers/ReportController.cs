using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.ReportService.Controllers
{
    [ApiController]
    [Route("api/v1/reports")]
    public class ReportController : ControllerBase
    {
        [HttpGet("export")]
        public async Task<IActionResult> ExportReport([FromQuery] string stationId = "All", [FromQuery] string? fromDate = null, [FromQuery] string? toDate = null)
        {
            // Dummy logic representing Elasticsearch aggregation query
            // In a real application, you would use Elastic.Clients.Elasticsearch to query your index.
            
            // Generate dummy statistics: Number of equipments by station
            var stats = new List<dynamic>
            {
                new { Station = "Trạm 110kV Hoàn Kiếm", Count = 150 },
                new { Station = "Trạm 110kV Ba Đình", Count = 120 },
                new { Station = "Trạm 220kV Tây Hồ", Count = 300 },
                new { Station = "Trạm 110kV Đống Đa", Count = 180 },
            };

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Thống kê thiết bị");

            // Header
            worksheet.Cell(1, 1).Value = "Tên Trạm";
            worksheet.Cell(1, 2).Value = "Số lượng thiết bị";

            // Format Header
            var headerRange = worksheet.Range("A1:B1");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            
            // Rows
            int row = 2;
            foreach (var stat in stats)
            {
                worksheet.Cell(row, 1).Value = stat.Station;
                worksheet.Cell(row, 2).Value = stat.Count;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            var fileName = $"ThongKeThietBi_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
