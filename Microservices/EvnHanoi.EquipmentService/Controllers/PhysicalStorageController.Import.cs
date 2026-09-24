using System.Security.Claims;
using ClosedXML.Excel;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Import hàng loạt Kệ/Tầng/Hộp từ file Excel — 1 file, 3 sheet theo đúng thứ tự phân cấp
/// (Kệ → Tầng → Hộp). Mã luôn do hệ thống tự sinh (giống tạo thủ công), Tầng/Hộp đối chiếu Kệ/Tầng
/// cha bằng TÊN (không phải Mã) vì Mã của 1 Kệ/Tầng vừa được tạo trong CÙNG file chưa thể biết trước.
/// </summary>
public partial class PhysicalStorageController
{
    private const string ImportSheetShelf = "Kệ";
    private const string ImportSheetFloor = "Tầng";
    private const string ImportSheetBox = "Hộp";

    [HttpGet("import/template")]
    public IActionResult DownloadImportTemplate()
    {
        var content = GenerateImportTemplate();
        var fileName = $"Mau_Import_Ke_Tang_Hop_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Tệp excel không hợp lệ hoặc rỗng." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".xlsx")
            return BadRequest(new { message = "Chỉ chấp nhận tệp định dạng .xlsx." });

        var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        var allowedUnitIds = await GetAllowedUnitIdsAsync();

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var result = new PhysicalStorageImportResultDto();

        // Cache đơn vị theo Mã (tránh tra DB lặp lại) + đối chiếu quyền theo đơn vị của từng dòng.
        var unitIdByCode = new Dictionary<string, long?>(StringComparer.OrdinalIgnoreCase);
        async Task<(long? UnitId, string? Error)> ResolveUnitAsync(string unitCode)
        {
            if (string.IsNullOrWhiteSpace(unitCode))
                return (null, "Thiếu Mã đơn vị.");

            if (!unitIdByCode.TryGetValue(unitCode, out var unitId))
            {
                unitId = await _repository.FindUnitIdByCodeAsync(unitCode);
                unitIdByCode[unitCode] = unitId;
            }
            if (unitId == null)
                return (null, $"Không tìm thấy đơn vị có Mã '{unitCode}'.");
            if (allowedUnitIds != null && !allowedUnitIds.Contains(unitId.Value))
                return (null, $"Bạn không có quyền tạo dữ liệu cho đơn vị '{unitCode}'.");

            return (unitId, null);
        }

        // (UnitId, Tên kệ viết hoa) -> ShelfId — gồm cả kệ đã có sẵn (tra khi cần) và kệ vừa tạo trong sheet Kệ.
        var shelfIdByUnitAndName = new Dictionary<(long UnitId, string Name), long>();
        // (ShelfId, Tên tầng viết hoa) -> FloorId — gồm cả tầng đã có sẵn và tầng vừa tạo trong sheet Tầng.
        var floorIdByShelfAndName = new Dictionary<(long ShelfId, string Name), long>();

        static string NormalizeName(string? name) => (name ?? string.Empty).Trim().ToUpperInvariant();

        // ── Sheet Kệ ──
        var shelfSheet = workbook.Worksheets.TryGetWorksheet(ImportSheetShelf, out var wsShelf) ? wsShelf : null;
        if (shelfSheet != null)
        {
            foreach (var row in shelfSheet.RowsUsed().Skip(1))
            {
                var unitCode = row.Cell(2).GetString().Trim();
                var name = row.Cell(3).GetString().Trim();
                var description = row.Cell(4).GetString().Trim();
                var priorityText = row.Cell(5).GetString().Trim();

                if (string.IsNullOrWhiteSpace(unitCode) && string.IsNullOrWhiteSpace(name))
                    continue; // dòng trống, bỏ qua

                var (unitId, unitError) = await ResolveUnitAsync(unitCode);
                if (unitError != null)
                {
                    result.Errors.Add(new PhysicalStorageImportErrorDto { Sheet = ImportSheetShelf, Row = row.RowNumber(), Reason = unitError });
                    continue;
                }
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Errors.Add(new PhysicalStorageImportErrorDto { Sheet = ImportSheetShelf, Row = row.RowNumber(), Reason = "Thiếu Tên kệ." });
                    continue;
                }

                var code = await _repository.GenerateNextShelfCodeAsync(unitId!.Value);
                if (code == null)
                {
                    result.Errors.Add(new PhysicalStorageImportErrorDto { Sheet = ImportSheetShelf, Row = row.RowNumber(), Reason = "Không thể sinh mã kệ cho đơn vị này." });
                    continue;
                }

                var shelf = new PhysicalShelf
                {
                    UnitId = unitId.Value,
                    Code = code,
                    Name = name,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    Priority = int.TryParse(priorityText, out var p) && p > 0 ? p : 1,
                    CreatedBy = userName
                };
                var shelfId = await _repository.CreateShelfAsync(shelf);
                shelfIdByUnitAndName[(unitId.Value, NormalizeName(name))] = shelfId;
                result.ShelvesCreated++;
            }
        }

        // ── Sheet Tầng ──
        var floorSheet = workbook.Worksheets.TryGetWorksheet(ImportSheetFloor, out var wsFloor) ? wsFloor : null;
        if (floorSheet != null)
        {
            foreach (var row in floorSheet.RowsUsed().Skip(1))
            {
                var unitCode = row.Cell(2).GetString().Trim();
                var shelfName = row.Cell(3).GetString().Trim();
                var name = row.Cell(4).GetString().Trim();
                var description = row.Cell(5).GetString().Trim();
                var priorityText = row.Cell(6).GetString().Trim();

                if (string.IsNullOrWhiteSpace(unitCode) && string.IsNullOrWhiteSpace(shelfName) && string.IsNullOrWhiteSpace(name))
                    continue;

                var (unitId, unitError) = await ResolveUnitAsync(unitCode);
                if (unitError != null)
                {
                    result.Errors.Add(new PhysicalStorageImportErrorDto { Sheet = ImportSheetFloor, Row = row.RowNumber(), Reason = unitError });
                    continue;
                }
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Errors.Add(new PhysicalStorageImportErrorDto { Sheet = ImportSheetFloor, Row = row.RowNumber(), Reason = "Thiếu Tên tầng." });
                    continue;
                }

                var shelfKey = (unitId!.Value, NormalizeName(shelfName));
                if (!shelfIdByUnitAndName.TryGetValue(shelfKey, out var shelfId))
                {
                    var found = await _repository.FindShelfIdByUnitAndNameAsync(unitId.Value, shelfName);
                    if (found == null)
                    {
                        result.Errors.Add(new PhysicalStorageImportErrorDto { Sheet = ImportSheetFloor, Row = row.RowNumber(), Reason = $"Không tìm thấy Kệ '{shelfName}' thuộc đơn vị '{unitCode}' (kiểm tra sheet Kệ nếu kệ này cũng đang được import)." });
                        continue;
                    }
                    shelfId = found.Value;
                    shelfIdByUnitAndName[shelfKey] = shelfId;
                }

                var code = await _repository.GenerateNextFloorCodeAsync(unitId.Value);
                if (code == null)
                {
                    result.Errors.Add(new PhysicalStorageImportErrorDto { Sheet = ImportSheetFloor, Row = row.RowNumber(), Reason = "Không thể sinh mã tầng cho đơn vị này." });
                    continue;
                }

                var floor = new PhysicalFloor
                {
                    ShelfId = shelfId,
                    Code = code,
                    Name = name,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    Priority = int.TryParse(priorityText, out var p) && p > 0 ? p : 1,
                    CreatedBy = userName
                };
                var floorId = await _repository.CreateFloorAsync(floor);
                floorIdByShelfAndName[(shelfId, NormalizeName(name))] = floorId;
                result.FloorsCreated++;
            }
        }

        // ── Sheet Hộp ──
        var boxSheet = workbook.Worksheets.TryGetWorksheet(ImportSheetBox, out var wsBox) ? wsBox : null;
        if (boxSheet != null)
        {
            foreach (var row in boxSheet.RowsUsed().Skip(1))
            {
                var unitCode = row.Cell(2).GetString().Trim();
                var shelfName = row.Cell(3).GetString().Trim();
                var floorName = row.Cell(4).GetString().Trim();
                var name = row.Cell(5).GetString().Trim();
                var description = row.Cell(6).GetString().Trim();
                var priorityText = row.Cell(7).GetString().Trim();

                if (string.IsNullOrWhiteSpace(unitCode) && string.IsNullOrWhiteSpace(shelfName)
                    && string.IsNullOrWhiteSpace(floorName) && string.IsNullOrWhiteSpace(name))
                    continue;

                var (unitId, unitError) = await ResolveUnitAsync(unitCode);
                if (unitError != null)
                {
                    result.Errors.Add(new PhysicalStorageImportErrorDto { Sheet = ImportSheetBox, Row = row.RowNumber(), Reason = unitError });
                    continue;
                }
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Errors.Add(new PhysicalStorageImportErrorDto { Sheet = ImportSheetBox, Row = row.RowNumber(), Reason = "Thiếu Tên hộp." });
                    continue;
                }

                var shelfKey = (unitId!.Value, NormalizeName(shelfName));
                if (!shelfIdByUnitAndName.TryGetValue(shelfKey, out var shelfId))
                {
                    var found = await _repository.FindShelfIdByUnitAndNameAsync(unitId.Value, shelfName);
                    if (found == null)
                    {
                        result.Errors.Add(new PhysicalStorageImportErrorDto { Sheet = ImportSheetBox, Row = row.RowNumber(), Reason = $"Không tìm thấy Kệ '{shelfName}' thuộc đơn vị '{unitCode}'." });
                        continue;
                    }
                    shelfId = found.Value;
                    shelfIdByUnitAndName[shelfKey] = shelfId;
                }

                var floorKey = (shelfId, NormalizeName(floorName));
                if (!floorIdByShelfAndName.TryGetValue(floorKey, out var floorId))
                {
                    var found = await _repository.FindFloorIdByShelfAndNameAsync(shelfId, floorName);
                    if (found == null)
                    {
                        result.Errors.Add(new PhysicalStorageImportErrorDto { Sheet = ImportSheetBox, Row = row.RowNumber(), Reason = $"Không tìm thấy Tầng '{floorName}' thuộc Kệ '{shelfName}' (kiểm tra sheet Tầng nếu tầng này cũng đang được import)." });
                        continue;
                    }
                    floorId = found.Value;
                    floorIdByShelfAndName[floorKey] = floorId;
                }

                var code = await _repository.GenerateNextBoxCodeAsync(unitId.Value);
                if (code == null)
                {
                    result.Errors.Add(new PhysicalStorageImportErrorDto { Sheet = ImportSheetBox, Row = row.RowNumber(), Reason = "Không thể sinh mã hộp cho đơn vị này." });
                    continue;
                }

                var box = new PhysicalBox
                {
                    FloorId = floorId,
                    Code = code,
                    Name = name,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    Priority = int.TryParse(priorityText, out var p) && p > 0 ? p : 1,
                    CreatedBy = userName
                };
                await _repository.CreateBoxAsync(box);
                result.BoxesCreated++;
            }
        }

        return Ok(result);
    }

    private static byte[] GenerateImportTemplate()
    {
        using var workbook = new XLWorkbook();

        void AddSheet(string sheetName, string[] headers)
        {
            var ws = workbook.Worksheets.Add(sheetName);
            for (var i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];
            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Columns().AdjustToContents();
        }

        AddSheet(ImportSheetShelf, new[] { "STT", "Mã đơn vị", "Tên kệ", "Mô tả", "Ưu tiên" });
        AddSheet(ImportSheetFloor, new[] { "STT", "Mã đơn vị", "Tên kệ", "Tên tầng", "Mô tả", "Ưu tiên" });
        AddSheet(ImportSheetBox, new[] { "STT", "Mã đơn vị", "Tên kệ", "Tên tầng", "Tên hộp", "Mô tả", "Ưu tiên" });

        var wsRules = workbook.Worksheets.Add("Quy tắc import");
        wsRules.Cell(1, 1).Value = "Cột";
        wsRules.Cell(1, 2).Value = "Bắt buộc";
        wsRules.Cell(1, 3).Value = "Quy tắc nhập dữ liệu";
        var rulesHeader = wsRules.Range(1, 1, 1, 3);
        rulesHeader.Style.Font.Bold = true;
        rulesHeader.Style.Fill.BackgroundColor = XLColor.LightGray;

        string[][] ruleRows =
        {
            new[] { "Mã đơn vị", "Có (cả 3 sheet)", "Mã đơn vị quản lý (cột Code trong Cơ cấu tổ chức), phải là đơn vị bạn có quyền quản lý." },
            new[] { "Tên kệ", "Có (sheet Tầng, Hộp)", "Phải khớp đúng Tên 1 Kệ đã có sẵn, HOẶC 1 dòng ở sheet 'Kệ' trong CÙNG file này (không phân biệt hoa/thường, khoảng trắng đầu/cuối)." },
            new[] { "Tên tầng", "Có (sheet Hộp)", "Phải khớp đúng Tên 1 Tầng đã có sẵn thuộc đúng Kệ ở trên, HOẶC 1 dòng ở sheet 'Tầng' trong CÙNG file này." },
            new[] { "Ưu tiên", "Không", "Số nguyên dương, số nhỏ hơn ưu tiên cao hơn. Bỏ trống thì mặc định là 1." },
            new[] { "Mã (Kệ/Tầng/Hộp)", "Tự sinh", "Hệ thống tự sinh theo quy tắc {Mã đơn vị}_KE/TANG/HOP{Số thứ tự}, không nhập trong file import." },
            new[] { "Thứ tự import", "Bắt buộc theo đúng thứ tự", "Phải nhập lần lượt sheet 'Kệ' trước, 'Tầng' sau, 'Hộp' sau cùng — vì Tầng cần Kệ đã tồn tại, Hộp cần Tầng đã tồn tại." }
        };
        for (var r = 0; r < ruleRows.Length; r++)
        {
            wsRules.Cell(r + 2, 1).Value = ruleRows[r][0];
            wsRules.Cell(r + 2, 2).Value = ruleRows[r][1];
            wsRules.Cell(r + 2, 3).Value = ruleRows[r][2];
        }
        var allRulesRange = wsRules.Range(1, 1, ruleRows.Length + 1, 3);
        allRulesRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        allRulesRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        wsRules.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
