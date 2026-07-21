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
        /// View 1: Biểu đồ thống kê số lượng Tài liệu, Trang tài liệu theo từng loại văn bản
        /// GET /api/v1/reports/statistics/dossier-by-document-type/chart-stats
        /// </summary>
        [HttpGet("dossier-by-document-type/chart-stats")]
        public async Task<IActionResult> GetDossierByDocumentTypeChartStats(
            [FromQuery] DossierByDocumentTypeFilterDto filter,
            [FromQuery(Name = "documentTypeIds")] string[]? documentTypeIds)
        {
            ApplyDocumentTypeIdsFilter(filter, documentTypeIds);
            var scope = ResolveUserScope();
            var stats = await _dossierRepository.GetDossierByDocumentTypeChartStatsAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(stats);
        }

        /// <summary>
        /// Tab Danh sách tài liệu — bảng có phân trang
        /// GET /api/v1/reports/statistics/dossier-by-document-type/list
        /// </summary>
        [HttpGet("dossier-by-document-type/list")]
        public async Task<IActionResult> GetDossierByDocumentTypeDocumentList(
            [FromQuery] DossierByDocumentTypeFilterDto filter,
            [FromQuery(Name = "documentTypeIds")] string[]? documentTypeIds)
        {
            ApplyDocumentTypeIdsFilter(filter, documentTypeIds);
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByDocumentTypeDocumentListAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// View 2: Lưới hồ sơ theo loại văn bản
        /// GET /api/v1/reports/statistics/dossier-by-document-type/document-type-grid
        /// </summary>
        [HttpGet("dossier-by-document-type/document-type-grid")]
        public async Task<IActionResult> GetDossierByDocumentTypeGrid(
            [FromQuery] DossierByDocumentTypeFilterDto filter,
            [FromQuery(Name = "documentTypeIds")] string[]? documentTypeIds)
        {
            ApplyDocumentTypeIdsFilter(filter, documentTypeIds);
            var scope = ResolveUserScope();
            var result = await _dossierRepository.GetDossierByDocumentTypeGridAsync(filter, scope.IsAdmin, scope.UnitId);
            return Ok(result);
        }

        /// <summary>
        /// Xuất Excel tab Danh sách tài liệu theo loại văn bản
        /// GET /api/v1/reports/statistics/dossier-by-document-type/export
        /// </summary>
        [HttpGet("dossier-by-document-type/export")]
        public async Task<IActionResult> ExportDossierByDocumentType(
            [FromQuery] DossierByDocumentTypeFilterDto filter,
            [FromQuery(Name = "documentTypeIds")] string[]? documentTypeIds)
        {
            ApplyDocumentTypeIdsFilter(filter, documentTypeIds);
            var scope = ResolveUserScope();
            filter.Page = 1;
            filter.PageSize = 10000;

            var data = await _dossierRepository.GetDossierByDocumentTypeDocumentListAsync(filter, scope.IsAdmin, scope.UnitId);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachTaiLieu");

            var yearLabel = filter.Year.HasValue && filter.Year.Value > 0
                ? $"NĂM {filter.Year.Value}"
                : "TẤT CẢ CÁC NĂM";
            const int totalCols = 6;
            worksheet.Cell(1, 1).Value = $"DANH SÁCH TÀI LIỆU XUẤT BẢN THEO LOẠI VĂN BẢN {yearLabel}";
            worksheet.Range(1, 1, 1, totalCols).Merge().Style.Font.SetBold().Font.SetFontSize(14)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var col = 1;
            const int headerRow = 3;
            worksheet.Cell(headerRow, col++).Value = "STT";
            worksheet.Cell(headerRow, col++).Value = "Tên loại văn bản";
            worksheet.Cell(headerRow, col++).Value = "Loại hồ sơ";
            worksheet.Cell(headerRow, col++).Value = "Trạm / Đường dây";
            worksheet.Cell(headerRow, col++).Value = "Thiết bị";
            worksheet.Cell(headerRow, col++).Value = "File tài liệu";

            var headerRange = worksheet.Range(headerRow, 1, headerRow, col - 1);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var row = 4;
            foreach (var item in data.Items)
            {
                col = 1;
                worksheet.Cell(row, col++).Value = item.Stt;
                worksheet.Cell(row, col++).Value = item.DocumentTypeName;
                worksheet.Cell(row, col++).Value = item.DossierTypeName;
                worksheet.Cell(row, col++).Value = item.InfrastructureName;
                worksheet.Cell(row, col++).Value = item.EquipmentName;
                worksheet.Cell(row, col++).Value = item.DocumentName;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileSuffix = filter.Year.HasValue && filter.Year.Value > 0
                ? $"Nam_{filter.Year.Value}"
                : "TatCaCacNam";
            var fileName = $"DanhSachTaiLieu_LoaiVanBan_{fileSuffix}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// Gom documentTypeIds từ query (lặp key hoặc chuỗi phân tách bằng dấu phẩy) vào filter DTO.
        /// </summary>
        private static void ApplyDocumentTypeIdsFilter(
            DossierByDocumentTypeFilterDto filter,
            string[]? documentTypeIds)
        {
            var merged = new List<string>();

            if (documentTypeIds is { Length: > 0 })
                merged.AddRange(documentTypeIds);

            if (filter.DocumentTypeIds is { Count: > 0 })
                merged.AddRange(filter.DocumentTypeIds);

            var normalized = merged
                .SelectMany(ParseDocumentTypeIdTokens)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            filter.DocumentTypeIds = normalized.Count > 0 ? normalized : null;
        }

        private static IEnumerable<string> ParseDocumentTypeIdTokens(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                yield break;

            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return token;
        }
    }
}
