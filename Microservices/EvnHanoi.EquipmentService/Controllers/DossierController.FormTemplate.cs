using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Biểu mẫu EAV theo ngữ cảnh hồ sơ — tránh gọi trực tiếp api/v1/eav-form-templates (quyền FORM).
/// </summary>
public abstract partial class DossierControllerBase
{
    /// <summary>Lấy EAV form template gắn với hồ sơ (hoặc formId loại văn bản) để xem/preview tài liệu.</summary>
    [HttpGet("{id:guid}/form-template")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetFormTemplate(Guid id, [FromQuery] Guid? formId = null)
    {
        try
        {
            var template = await _dossierService.GetFormTemplateForDossierAsync(id, formId);
            if (template is null)
                return NotFound(new { message = "Không tìm thấy biểu mẫu EAV cho hồ sơ này." });

            return Ok(template);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
