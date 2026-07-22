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
        /// View 1: 3 box KPI — lượt tra cứu hồ sơ TBA / hồ sơ đường dây / tài liệu (qua tìm kiếm fulltext),
        /// mỗi box kèm % tăng trưởng so với tháng trước.
        /// GET /api/v1/reports/statistics/dossier-most-viewed/summary-stats
        /// </summary>
        [HttpGet("dossier-most-viewed/summary-stats")]
        public async Task<IActionResult> GetDossierMostViewedSummaryStats([FromQuery] DossierMostViewedFilterDto filter)
        {
            var scope = ResolveUserScope();
            var stats = await _dossierRepository.GetDossierMostViewedSummaryStatsAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(stats);
        }

        /// <summary>
        /// View 2: Lưới hồ sơ — mỗi dòng 1 hồ sơ kèm tổng số lượt tra cứu, sắp xếp giảm dần.
        /// GET /api/v1/reports/statistics/dossier-most-viewed/grid
        /// </summary>
        [HttpGet("dossier-most-viewed/grid")]
        public async Task<IActionResult> GetDossierMostViewedGrid([FromQuery] DossierMostViewedFilterDto filter)
        {
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierMostViewedGridAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// Xuất Excel lưới hồ sơ được tra cứu nhiều nhất
        /// GET /api/v1/reports/statistics/dossier-most-viewed/export
        /// </summary>
        [HttpGet("dossier-most-viewed/export")]
        public async Task<IActionResult> ExportDossierMostViewed([FromQuery] DossierMostViewedFilterDto filter)
        {
            var scope = ResolveUserScope();
            filter.Page = 1;
            filter.PageSize = 10000;

            var bhsColumns = (await _dossierRepository.GetBhsColumnsAsync()).ToList();
            var data = await _dossierRepository.GetDossierMostViewedGridAsync(filter, scope.IsAdmin, scope.UnitId);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("LuotTraCuu");

            var totalCols = 1 + bhsColumns.Count + 2;
            worksheet.Cell(1, 1).Value = "DANH SÁCH HỒ SƠ ĐƯỢC TRA CỨU NHIỀU NHẤT";
            worksheet.Range(1, 1, 1, totalCols).Merge().Style.Font.SetBold().Font.SetFontSize(14)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var col = 1;
            const int headerRow = 3;
            worksheet.Cell(headerRow, col++).Value = "STT";
            foreach (var bhs in bhsColumns)
                worksheet.Cell(headerRow, col++).Value = bhs.Label;
            worksheet.Cell(headerRow, col++).Value = "Trạm / Đường dây";
            worksheet.Cell(headerRow, col++).Value = "Số lượt tra cứu";

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
                worksheet.Cell(row, col++).Value = item.ViewCount;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"HoSoTraCuuNhieuNhat_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
