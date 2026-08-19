using Microsoft.AspNetCore.Mvc;
using EvnHanoi.Infrastructure.Audit;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Ký số tài liệu hồ sơ (tích hợp API ký số ngoài) — partial của DossierControllerBase.
/// Quyền tự động suy ra là DOSSIER_SIGN (xem PermissionCodeResolver.CategorizeAction: action chứa
/// "sign" → category SIGN).
/// </summary>
public abstract partial class DossierControllerBase
{
    [HttpPost("{id:guid}/documents/{documentId:guid}/sign")]
    public async Task<IActionResult> SignDocument(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _dossierDocumentService.SignDocumentAsync(
                id, documentId, UserId, UserFullName, GetUserUnitId(), cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new { message = result.ErrorMessage ?? "Ký số thất bại." });
            }

            HttpContext.SetAudit(
                resourceId: documentId.ToString(),
                resourceType: "DOCUMENT",
                action: AuditActions.Update);

            return Ok(new
            {
                message = "Ký số tài liệu thành công.",
                newVersionId = result.NewVersionId,
                newVersionNumber = result.NewVersionNumber,
                signedAt = result.SignedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
