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
        /// View 1: Biểu đồ thống kê số lượng Hồ sơ, Tài liệu, Trang tài liệu theo từng loại hồ sơ
        /// GET /api/v1/reports/statistics/dossier-by-dossier-type/chart-stats
        /// </summary>
        [HttpGet("dossier-by-dossier-type/chart-stats")]
        public async Task<IActionResult> GetDossierByDossierTypeChartStats(
            [FromQuery] DossierByDossierTypeFilterDto filter,
            [FromQuery(Name = "dossierTypeIds")] string[]? dossierTypeIds)
        {
            ApplyDossierTypeIdsFilter(filter, dossierTypeIds);
            var scope = ResolveUserScope();
            var stats = await _dossierRepository.GetDossierByDossierTypeChartStatsAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(stats);
        }

        /// <summary>
        /// Tab Danh sách hồ sơ — bảng có phân trang
        /// GET /api/v1/reports/statistics/dossier-by-dossier-type/list
        /// </summary>
        [HttpGet("dossier-by-dossier-type/list")]
        public async Task<IActionResult> GetDossierByDossierTypeList(
            [FromQuery] DossierByDossierTypeFilterDto filter,
            [FromQuery(Name = "dossierTypeIds")] string[]? dossierTypeIds)
        {
            ApplyDossierTypeIdsFilter(filter, dossierTypeIds);
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByDossierTypeListAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// View 2: Lưới hồ sơ theo loại hồ sơ
        /// GET /api/v1/reports/statistics/dossier-by-dossier-type/dossier-type-grid
        /// </summary>
        [HttpGet("dossier-by-dossier-type/dossier-type-grid")]
        public async Task<IActionResult> GetDossierByDossierTypeGrid(
            [FromQuery] DossierByDossierTypeFilterDto filter,
            [FromQuery(Name = "dossierTypeIds")] string[]? dossierTypeIds)
        {
            ApplyDossierTypeIdsFilter(filter, dossierTypeIds);
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByDossierTypeGridAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// Xuất Excel tab Danh sách hồ sơ theo loại hồ sơ
        /// GET /api/v1/reports/statistics/dossier-by-dossier-type/export
        /// </summary>
        [HttpGet("dossier-by-dossier-type/export")]
        public async Task<IActionResult> ExportDossierByDossierType(
            [FromQuery] DossierByDossierTypeFilterDto filter,
            [FromQuery(Name = "dossierTypeIds")] string[]? dossierTypeIds)
        {
            ApplyDossierTypeIdsFilter(filter, dossierTypeIds);
            var scope = ResolveUserScope();
            filter.Page = 1;
            filter.PageSize = 10000;

            var bhsColumns = (await _dossierRepository.GetBhsColumnsAsync()).ToList();
            var data = await _dossierRepository.GetDossierByDossierTypeListAsync(filter, scope.IsAdmin, scope.UnitId);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachHoSo");

            var yearLabel = filter.Year.HasValue && filter.Year.Value > 0
                ? $"NĂM {filter.Year.Value}"
                : "TẤT CẢ CÁC NĂM";
            var totalCols = 1 + bhsColumns.Count + 3;
            worksheet.Cell(1, 1).Value = $"DANH SÁCH HỒ SƠ XUẤT BẢN THEO LOẠI HỒ SƠ {yearLabel}";
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
            var fileName = $"DanhSachHoSo_LoaiHoSo_{fileSuffix}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// Gom dossierTypeIds từ query (lặp key hoặc chuỗi phân tách bằng dấu phẩy) vào filter DTO.
        /// </summary>
        private static void ApplyDossierTypeIdsFilter(
            DossierByDossierTypeFilterDto filter,
            string[]? dossierTypeIds)
        {
            var merged = new List<string>();

            if (dossierTypeIds is { Length: > 0 })
                merged.AddRange(dossierTypeIds);

            if (filter.DossierTypeIds is { Count: > 0 })
                merged.AddRange(filter.DossierTypeIds);

            var normalized = merged
                .SelectMany(ParseDossierTypeIdTokens)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            filter.DossierTypeIds = normalized.Count > 0 ? normalized : null;
        }

        private static IEnumerable<string> ParseDossierTypeIdTokens(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                yield break;

            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return token;
        }
    }
}
