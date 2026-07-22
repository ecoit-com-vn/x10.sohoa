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
        /// View 1: Biểu đồ cột ngang — số lượng Hồ sơ/Tài liệu/Trang theo từng loại thiết bị.
        /// GET /api/v1/reports/statistics/dossier-by-manufacture-year/chart-stats
        /// </summary>
        [HttpGet("dossier-by-manufacture-year/chart-stats")]
        public async Task<IActionResult> GetDossierByManufactureYearChartStats(
            [FromQuery] DossierByManufactureYearFilterDto filter,
            [FromQuery(Name = "stationIds")] string[]? stationIds)
        {
            ApplyManufactureYearStationIdsFilter(filter, stationIds);
            var scope = ResolveUserScope();
            var stats = await _dossierRepository.GetDossierByManufactureYearChartStatsAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(stats);
        }

        /// <summary>
        /// Tab Danh sách hồ sơ — bảng có phân trang
        /// GET /api/v1/reports/statistics/dossier-by-manufacture-year/list
        /// </summary>
        [HttpGet("dossier-by-manufacture-year/list")]
        public async Task<IActionResult> GetDossierByManufactureYearList(
            [FromQuery] DossierByManufactureYearFilterDto filter,
            [FromQuery(Name = "stationIds")] string[]? stationIds)
        {
            ApplyManufactureYearStationIdsFilter(filter, stationIds);
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByManufactureYearListAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// View 2: Lưới hồ sơ theo thiết bị
        /// GET /api/v1/reports/statistics/dossier-by-manufacture-year/equipment-grid
        /// </summary>
        [HttpGet("dossier-by-manufacture-year/equipment-grid")]
        public async Task<IActionResult> GetDossierByManufactureYearEquipmentGrid(
            [FromQuery] DossierByManufactureYearFilterDto filter,
            [FromQuery(Name = "stationIds")] string[]? stationIds)
        {
            ApplyManufactureYearStationIdsFilter(filter, stationIds);
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByManufactureYearEquipmentGridAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// Xuất Excel tab Danh sách hồ sơ theo năm sản xuất thiết bị
        /// GET /api/v1/reports/statistics/dossier-by-manufacture-year/export
        /// </summary>
        [HttpGet("dossier-by-manufacture-year/export")]
        public async Task<IActionResult> ExportDossierByManufactureYear(
            [FromQuery] DossierByManufactureYearFilterDto filter,
            [FromQuery(Name = "stationIds")] string[]? stationIds)
        {
            ApplyManufactureYearStationIdsFilter(filter, stationIds);
            var scope = ResolveUserScope();
            filter.Page = 1;
            filter.PageSize = 10000;

            var bhsColumns = (await _dossierRepository.GetBhsColumnsAsync()).ToList();
            var data = await _dossierRepository.GetDossierByManufactureYearListAsync(filter, scope.IsAdmin, scope.UnitId);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachHoSo");

            var yearLabel = filter.ManufactureYear.HasValue && filter.ManufactureYear.Value > 0
                ? $"NĂM SẢN XUẤT {filter.ManufactureYear.Value}"
                : "TẤT CẢ CÁC NĂM";
            var totalCols = 1 + bhsColumns.Count + 3;
            worksheet.Cell(1, 1).Value = $"DANH SÁCH HỒ SƠ XUẤT BẢN THEO NĂM SẢN XUẤT THIẾT BỊ {yearLabel}";
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
            var fileSuffix = filter.ManufactureYear.HasValue && filter.ManufactureYear.Value > 0
                ? $"NamSanXuat_{filter.ManufactureYear.Value}"
                : "TatCaCacNam";
            var fileName = $"DanhSachHoSo_NamSanXuatThietBi_{fileSuffix}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static void ApplyManufactureYearStationIdsFilter(
            DossierByManufactureYearFilterDto filter,
            string[]? stationIds)
        {
            var merged = new List<string>();

            if (stationIds is { Length: > 0 })
                merged.AddRange(stationIds);

            if (filter.StationIds is { Count: > 0 })
                merged.AddRange(filter.StationIds);

            var normalized = merged
                .SelectMany(raw => (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            filter.StationIds = normalized.Count > 0 ? normalized : null;
        }
    }
}
