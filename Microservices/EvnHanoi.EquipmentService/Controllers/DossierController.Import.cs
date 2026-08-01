using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.Infrastructure.Audit;
using EvnHanoi.Infrastructure.Security;

namespace EvnHanoi.EquipmentService.Controllers;

public abstract partial class DossierControllerBase
{
    [HttpGet("next-code")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetNextDossierCode(
        [FromQuery] Guid? infrastructureId,
        [FromQuery] Guid? dossierTypeId)
    {
        if (!infrastructureId.HasValue || !dossierTypeId.HasValue)
            return BadRequest(new { message = "Trạm / đường dây và loại hồ sơ là bắt buộc." });

        try
        {
            return Ok(await _dossierService.GenerateDossierCodeAsync(
                infrastructureId.Value,
                dossierTypeId.Value));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("import/template")]
    public IActionResult DownloadImportTemplate()
    {
        try
        {
            var content = _dossierService.GenerateImportTemplate();
            var fileName = $"Mau_Import_Ho_So_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Lỗi khi xuất file mẫu: {ex.Message}" });
        }
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportDossiers(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Tệp excel không hợp lệ hoặc rỗng." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".xlsx")
        {
            return BadRequest(new { message = "Chỉ chấp nhận tệp định dạng .xlsx." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _dossierService.ImportDossiersAsync(
                stream,
                UserId,
                UserName,
                UserFullName,
                ExpectedKindId
            );

            HttpContext.SetAudit(
                null,
                null,
                $"Import hồ sơ từ Excel (Thành công: {result.SuccessDossiers.Count}, Thất bại: {result.FailedDossiers.Count})",
                "DOSSIER",
                AuditActions.Create
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Lỗi hệ thống khi import: {ex.Message}" });
        }
    }
}
