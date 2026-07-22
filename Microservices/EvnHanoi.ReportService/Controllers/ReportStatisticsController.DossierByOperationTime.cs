using System;
using System.Collections.Generic;
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
        /// View 1: 3 box KPI — tổng hồ sơ / tài liệu / trang theo bộ lọc (không có % tăng trưởng).
        /// GET /api/v1/reports/statistics/dossier-by-operation-time/summary-stats
        /// </summary>
        [HttpGet("dossier-by-operation-time/summary-stats")]
        public async Task<IActionResult> GetDossierByOperationTimeSummaryStats([FromQuery] DossierByOperationTimeFilterDto filter)
        {
            var scope = ResolveUserScope();
            var stats = await _dossierRepository.GetDossierByOperationTimeSummaryStatsAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(stats);
        }

        /// <summary>
        /// Tab Danh sách hồ sơ — bảng có phân trang
        /// GET /api/v1/reports/statistics/dossier-by-operation-time/list
        /// </summary>
        [HttpGet("dossier-by-operation-time/list")]
        public async Task<IActionResult> GetDossierByOperationTimeList([FromQuery] DossierByOperationTimeFilterDto filter)
        {
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByOperationTimeListAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// View 2: Lưới hồ sơ theo trạm/đường dây (thời gian vận hành)
        /// GET /api/v1/reports/statistics/dossier-by-operation-time/station-grid
        /// </summary>
        [HttpGet("dossier-by-operation-time/station-grid")]
        public async Task<IActionResult> GetDossierByOperationTimeGrid([FromQuery] DossierByOperationTimeFilterDto filter)
        {
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByOperationTimeStationGridAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// Xuất Excel tab Danh sách hồ sơ theo thời gian vận hành
        /// GET /api/v1/reports/statistics/dossier-by-operation-time/export
        /// </summary>
        [HttpGet("dossier-by-operation-time/export")]
        public async Task<IActionResult> ExportDossierByOperationTime([FromQuery] DossierByOperationTimeFilterDto filter)
        {
            var scope = ResolveUserScope();
            filter.Page = 1;
            filter.PageSize = 10000;

            var bhsColumns = (await _dossierRepository.GetBhsColumnsAsync()).ToList();
            var data = await _dossierRepository.GetDossierByOperationTimeListAsync(filter, scope.IsAdmin, scope.UnitId);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachHoSo");

            var rangeLabel = filter.FromDate.HasValue || filter.ToDate.HasValue
                ? $"TỪ {(filter.FromDate.HasValue ? filter.FromDate.Value.ToString("dd/MM/yyyy") : "...")} ĐẾN {(filter.ToDate.HasValue ? filter.ToDate.Value.ToString("dd/MM/yyyy") : "...")}"
                : "TẤT CẢ";
            var totalCols = 1 + bhsColumns.Count + 3;
            worksheet.Cell(1, 1).Value = $"DANH SÁCH HỒ SƠ XUẤT BẢN THEO THỜI GIAN VẬN HÀNH {rangeLabel}";
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
            var fileName = $"DanhSachHoSo_ThoiGianVanHanh_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
