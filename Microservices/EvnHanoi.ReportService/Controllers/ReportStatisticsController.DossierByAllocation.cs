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
        /// View 1: Biểu đồ thống kê số lượng Hồ sơ, Tài liệu, Trang tài liệu theo phân bổ hồ sơ.
        /// GET /api/v1/reports/statistics/dossier-by-allocation/chart-stats
        /// </summary>
        [HttpGet("dossier-by-allocation/chart-stats")]
        public async Task<IActionResult> GetDossierByAllocationChartStats([FromQuery] DossierByAllocationFilterDto filter)
        {
            var scope = ResolveUserScope();
            var stats = await _dossierRepository.GetDossierByAllocationChartStatsAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(stats);
        }

        /// <summary>
        /// View 2: Thống kê tỷ lệ % theo số lượng hồ sơ giữa 3 nhóm.
        /// GET /api/v1/reports/statistics/dossier-by-allocation/ratio-stats
        /// </summary>
        [HttpGet("dossier-by-allocation/ratio-stats")]
        public async Task<IActionResult> GetDossierByAllocationRatioStats([FromQuery] DossierByAllocationFilterDto filter)
        {
            var scope = ResolveUserScope();
            var stats = await _dossierRepository.GetDossierByAllocationRatioStatsAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(stats);
        }

        /// <summary>
        /// Tab Danh sách hồ sơ — bảng có phân trang.
        /// GET /api/v1/reports/statistics/dossier-by-allocation/list
        /// </summary>
        [HttpGet("dossier-by-allocation/list")]
        public async Task<IActionResult> GetDossierByAllocationList([FromQuery] DossierByAllocationFilterDto filter)
        {
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByAllocationListAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// View 3: Lưới thống kê theo người tạo hồ sơ.
        /// GET /api/v1/reports/statistics/dossier-by-allocation/creator-grid
        /// </summary>
        [HttpGet("dossier-by-allocation/creator-grid")]
        public async Task<IActionResult> GetDossierByAllocationCreatorGrid([FromQuery] DossierByAllocationFilterDto filter)
        {
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByAllocationCreatorGridAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// Xuất Excel tab Danh sách hồ sơ.
        /// GET /api/v1/reports/statistics/dossier-by-allocation/export
        /// </summary>
        [HttpGet("dossier-by-allocation/export")]
        public async Task<IActionResult> ExportDossierByAllocation([FromQuery] DossierByAllocationFilterDto filter)
        {
            var scope = ResolveUserScope();
            filter.Page = 1;
            filter.PageSize = 10000;

            var bhsColumns = (await _dossierRepository.GetBhsColumnsAsync()).ToList();
            var data = await _dossierRepository.GetDossierByAllocationListAsync(filter, scope.IsAdmin, scope.UnitId);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachHoSo");

            var yearLabel = filter.Year.HasValue && filter.Year.Value > 0
                ? $"NĂM {filter.Year.Value}"
                : "TẤT CẢ CÁC NĂM";
            var totalCols = 1 + bhsColumns.Count + 3;
            worksheet.Cell(1, 1).Value = $"DANH SÁCH HỒ SƠ NHẬP LIỆU THEO PHÂN BỔ {yearLabel}";
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
            var fileName = $"DanhSachHoSo_PhanBo_{fileSuffix}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
