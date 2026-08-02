using System.Text.Json;
using EvnHanoi.DigitizationService.Models.OcrModule;
using EvnHanoi.DigitizationService.Repositories.OcrModule;
using EvnHanoi.Infrastructure.Database;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.DigitizationService.Controllers.OcrModule;

public class CreateTemplateSnapshotRequest
{
    public string Name { get; set; } = string.Empty;
    public string? DocumentTypeCode { get; set; }
    public string SourceJobId { get; set; } = string.Empty;
}

/// <summary>Yêu cầu 90 — quản lý mẫu tham chiếu (snapshot layout) dùng để so sánh sai khác.</summary>
[ApiController]
[Route("api/v1/ocr-module/templates")]
public class OcrModuleTemplateController : ControllerBase
{
    private readonly IOcrModuleRepository _repository;

    public OcrModuleTemplateController(IOcrModuleRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<List<OcrModuleTemplateSnapshot>>> GetTemplates([FromQuery] string? documentTypeCode)
    {
        var templates = await _repository.GetTemplateSnapshotsAsync(documentTypeCode);
        return Ok(templates);
    }

    /// <summary>Lưu layout hiện tại của 1 Job làm mẫu tham chiếu chuẩn.</summary>
    [HttpPost]
    public async Task<ActionResult<OcrModuleTemplateSnapshot>> CreateTemplate([FromBody] CreateTemplateSnapshotRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.SourceJobId))
        {
            return BadRequest(new { code = "ERR_OCR_MODULE_INVALID_TEMPLATE", message = "Thiếu tên mẫu hoặc Job nguồn." });
        }

        var regions = await _repository.GetAllRegionsAsync(request.SourceJobId);
        if (regions.Count == 0)
        {
            return BadRequest(new { code = "ERR_OCR_MODULE_TEMPLATE_NO_REGIONS", message = "Job nguồn chưa có vùng văn bản nào để lưu làm mẫu." });
        }

        var referenceRegions = regions
            .Where(r => r.RegionType == "Text")
            .Select(r => new TemplateRegionSnapshot
            {
                PageNumber = r.PageNumber,
                BoxX0 = r.BoxX0,
                BoxY0 = r.BoxY0,
                BoxX1 = r.BoxX1,
                BoxY1 = r.BoxY1,
                Text = r.TextRaw,
            })
            .ToList();

        var snapshot = new OcrModuleTemplateSnapshot
        {
            Id = UuidHelper.NewUuid(),
            Name = request.Name,
            DocumentTypeCode = request.DocumentTypeCode,
            SourceJobId = request.SourceJobId,
            ReferenceRegionsJson = JsonSerializer.Serialize(referenceRegions),
            CreatedBy = User?.Identity?.Name ?? "System",
        };

        await _repository.CreateTemplateSnapshotAsync(snapshot);

        return Ok(snapshot);
    }
}
