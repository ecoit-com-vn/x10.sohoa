using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Lịch sử gọi ký số (bảng DOCUMENT_SIGN_HISTORY, Migration0050) — tab "Lịch sử ký số" trên màn
/// Thiết lập đồng bộ ký số. Chỉ đọc — dữ liệu được ghi tự động bởi DossierDocumentService.SignDocumentAsync.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/digital-signature/sign-history")]
public class DigitalSignatureSignHistoryController : ControllerBase
{
    private readonly IDocumentRepository _documentRepository;

    public DigitalSignatureSignHistoryController(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var (items, totalCount) = await _documentRepository.GetDocumentSignHistoryPagedAsync(page, pageSize, keyword, status, fromDate, toDate);
        return Ok(new { items, totalCount, page, pageSize });
    }
}
