using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// Màn hình giám sát job OCR/bóc tách toàn hệ thống — chỉ đọc (danh sách + lọc + phân trang).
/// Hành động "Chạy lại" tái sử dụng nguyên các endpoint submit-digitization/rerun-extraction
/// đã có sẵn trên DossierController.Documents / EquipmentController.Documents — không có ở đây.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/ocr-jobs")]
public class OcrJobsMonitorController : ControllerBase
{
    private readonly IDocumentDigitizationRepository _repository;

    public OcrJobsMonitorController(IDocumentDigitizationRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? phase = null,
        [FromQuery] string? keyword = null,
        [FromQuery] Guid? documentTypeId = null,
        [FromQuery] string? resourceKeyword = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var filter = new OcrJobListFilter
        {
            Page = page,
            PageSize = pageSize,
            Status = status,
            Phase = phase,
            Keyword = keyword,
            DocumentTypeId = documentTypeId,
            ResourceKeyword = resourceKeyword,
            FromDate = fromDate,
            ToDate = toDate,
        };

        var (items, totalCount) = await _repository.GetJobsPagedAsync(filter);
        return Ok(new { items, totalCount, page, pageSize });
    }
}
