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
        /// View 1: Biểu đồ thống kê số lượng Hồ sơ, Tài liệu, Trang tài liệu theo từng hộp lưu trữ
        /// GET /api/v1/reports/statistics/dossier-by-box/chart-stats
        /// </summary>
        [HttpGet("dossier-by-box/chart-stats")]
        public async Task<IActionResult> GetDossierByBoxChartStats(
            [FromQuery] DossierByBoxFilterDto filter,
            [FromQuery(Name = "boxIds")] string[]? boxIds)
        {
            ApplyBoxIdsFilter(filter, boxIds);
            var scope = ResolveUserScope();
            var stats = await _dossierRepository.GetDossierByBoxChartStatsAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(stats);
        }

        /// <summary>
        /// Tab Danh sách hồ sơ — bảng có phân trang
        /// GET /api/v1/reports/statistics/dossier-by-box/list
        /// </summary>
        [HttpGet("dossier-by-box/list")]
        public async Task<IActionResult> GetDossierByBoxList(
            [FromQuery] DossierByBoxFilterDto filter,
            [FromQuery(Name = "boxIds")] string[]? boxIds)
        {
            ApplyBoxIdsFilter(filter, boxIds);
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByBoxListAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// View 2: Lưới hồ sơ theo hộp lưu trữ
        /// GET /api/v1/reports/statistics/dossier-by-box/box-grid
        /// </summary>
        [HttpGet("dossier-by-box/box-grid")]
        public async Task<IActionResult> GetDossierByBoxGrid(
            [FromQuery] DossierByBoxFilterDto filter,
            [FromQuery(Name = "boxIds")] string[]? boxIds)
        {
            ApplyBoxIdsFilter(filter, boxIds);
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByBoxGridAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// Xuất Excel tab Danh sách hồ sơ theo hộp lưu trữ
        /// GET /api/v1/reports/statistics/dossier-by-box/export
        /// </summary>
        [HttpGet("dossier-by-box/export")]
        public async Task<IActionResult> ExportDossierByBox(
            [FromQuery] DossierByBoxFilterDto filter,
            [FromQuery(Name = "boxIds")] string[]? boxIds)
        {
            ApplyBoxIdsFilter(filter, boxIds);
            var scope = ResolveUserScope();
            filter.Page = 1;
            filter.PageSize = 10000;

            var bhsColumns = (await _dossierRepository.GetBhsColumnsAsync()).ToList();
            var data = await _dossierRepository.GetDossierByBoxListAsync(filter, scope.IsAdmin, scope.UnitId);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachHoSo");

            var yearLabel = filter.Year.HasValue && filter.Year.Value > 0
                ? $"NĂM {filter.Year.Value}"
                : "TẤT CẢ CÁC NĂM";
            var totalCols = 1 + bhsColumns.Count + 3;
            worksheet.Cell(1, 1).Value = $"DANH SÁCH HỒ SƠ XUẤT BẢN THEO HỘP LƯU TRỮ {yearLabel}";
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
            var fileSuffix = filter.Year.HasValue && filter.Year.Value > 0
                ? $"Nam_{filter.Year.Value}"
                : "TatCaCacNam";
            var fileName = $"DanhSachHoSo_HopLuuTru_{fileSuffix}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// Gom boxIds từ query (lặp key hoặc chuỗi phân tách bằng dấu phẩy) vào filter DTO.
        /// </summary>
        private static void ApplyBoxIdsFilter(
            DossierByBoxFilterDto filter,
            string[]? boxIds)
        {
            var merged = new List<string>();

            if (boxIds is { Length: > 0 })
                merged.AddRange(boxIds);

            if (filter.BoxIds is { Count: > 0 })
                merged.AddRange(filter.BoxIds);

            var normalized = merged
                .SelectMany(ParseBoxIdTokens)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            filter.BoxIds = normalized.Count > 0 ? normalized : null;
        }

        private static IEnumerable<string> ParseBoxIdTokens(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                yield break;

            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return token;
        }
    }
}
