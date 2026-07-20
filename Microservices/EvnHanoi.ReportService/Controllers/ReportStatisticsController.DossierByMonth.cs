using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using EvnHanoi.ReportService.Core.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.ReportService.Controllers
{
    public partial class ReportStatisticsController
    {
        /// <summary>
        /// View 1: Biểu đồ thống kê số lượng Hồ sơ, Tài liệu, Trang tài liệu theo tháng
        /// GET /api/v1/reports/statistics/dossier-by-month/chart-stats
        /// </summary>
        [HttpGet("dossier-by-month/chart-stats")]
        public async Task<IActionResult> GetDossierByMonthChartStats([FromQuery] DossierByMonthFilterDto filter)
        {
            var scope = ResolveUserScope();
            var stats = await _dossierRepository.GetDossierByMonthChartStatsAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(stats);
        }

        /// <summary>
        /// View 2: Thống kê tỷ lệ % theo số lượng hồ sơ theo tháng
        /// GET /api/v1/reports/statistics/dossier-by-month/ratio-stats
        /// </summary>
        [HttpGet("dossier-by-month/ratio-stats")]
        public async Task<IActionResult> GetDossierByMonthRatioStats([FromQuery] DossierByMonthFilterDto filter)
        {
            var scope = ResolveUserScope();
            var stats = await _dossierRepository.GetDossierByMonthRatioStatsAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(stats);
        }

        /// <summary>
        /// Tab Danh sách hồ sơ — bảng có phân trang theo tháng
        /// GET /api/v1/reports/statistics/dossier-by-month/list
        /// </summary>
        [HttpGet("dossier-by-month/list")]
        public async Task<IActionResult> GetDossierByMonthList([FromQuery] DossierByMonthFilterDto filter)
        {
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByMonthListAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// View 3: Lưới thống kê theo trạm/đường dây theo tháng
        /// GET /api/v1/reports/statistics/dossier-by-month/station-grid
        /// </summary>
        [HttpGet("dossier-by-month/station-grid")]
        public async Task<IActionResult> GetDossierByMonthStationGrid([FromQuery] DossierByMonthFilterDto filter)
        {
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByMonthStationGridAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// Xuất Excel tab Danh sách hồ sơ theo tháng
        /// GET /api/v1/reports/statistics/dossier-by-month/export
        /// </summary>
        [HttpGet("dossier-by-month/export")]
        public async Task<IActionResult> ExportDossierByMonth([FromQuery] DossierByMonthFilterDto filter)
        {
            var scope = ResolveUserScope();
            filter.Page = 1;
            filter.PageSize = 10000;

            var bhsColumns = (await _dossierRepository.GetBhsColumnsAsync()).ToList();
            var data = await _dossierRepository.GetDossierByMonthListAsync(filter, scope.IsAdmin, scope.UnitId);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachHoSo");

            var (year, month) = ResolveMonthFilter(filter);
            var totalCols = 1 + bhsColumns.Count + 3;
            worksheet.Cell(1, 1).Value = $"DANH SÁCH HỒ SƠ NHẬP LIỆU THÁNG {month:D2}/{year}";
            worksheet.Range(1, 1, 1, totalCols).Merge().Style.Font.SetBold().Font.SetFontSize(14)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var col = 1;
            const int headerRow = 3;
            worksheet.Cell(headerRow, col++).Value = "STT";
            foreach (var bhs in bhsColumns)
                worksheet.Cell(headerRow, col++).Value = bhs.Label;
            worksheet.Cell(headerRow, col++).Value = "Trạm / Đường dây";
            worksheet.Cell(headerRow, col++).Value = "Loại hồ sơ";
            worksheet.Cell(headerRow, col++).Value = "Số tài liệu";

            var headerRange = worksheet.Range(headerRow, 1, headerRow, col - 1);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var row = 4;
            foreach (var item in data.Items)
            {
                col = 1;
                worksheet.Cell(row, col++).Value = item.Stt;
                foreach (var bhs in bhsColumns)
                {
                    worksheet.Cell(row, col++).Value = item.CatalogData.TryGetValue(bhs.Key, out var val) ? val : "-";
                }
                worksheet.Cell(row, col++).Value = item.InfrastructureName;
                worksheet.Cell(row, col++).Value = item.DossierTypeName;
                worksheet.Cell(row, col++).Value = item.DocumentCount;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"DanhSachHoSo_Thang_{month:D2}_{year}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static (int Year, int Month) ResolveMonthFilter(DossierByMonthFilterDto filter)
        {
            var year = filter.Year.HasValue && filter.Year.Value > 0 ? filter.Year.Value : DateTime.Now.Year;
            var month = filter.Month.HasValue && filter.Month.Value is >= 1 and <= 12
                ? filter.Month.Value
                : DateTime.Now.Month;
            return (year, month);
        }
    }
}
