using System.Text.Json;
using EvnHanoi.DigitizationService.Core.Analysis;
using EvnHanoi.DigitizationService.Core.Services.OcrModule;
using EvnHanoi.DigitizationService.Models.OcrModule;
using EvnHanoi.DigitizationService.Repositories.OcrModule;
using EvnHanoi.Infrastructure.Database;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.DigitizationService.Controllers.OcrModule;

/// <summary>
/// Các API phân tích Nhóm A trên Job (93 Loại chữ viết, 88 Công thức, 94 Con dấu/chữ ký, 90 So sánh mẫu,
/// 95 Chính tả, 92 Phân tích lỗi) — đều xử lý bằng C# thuần trên dữ liệu <c>OCR_MODULE_REGION</c> đã
/// materialize sẵn, không gọi lại ocr_vl_server.
/// </summary>
[ApiController]
[Route("api/v1/ocr-module/jobs/{jobId}")]
public class OcrModuleAnalysisController : ControllerBase
{
    private readonly IOcrModuleRepository _repository;
    private readonly IOcrModuleSealSignatureService _sealSignatureService;
    private readonly IOcrModuleSpellcheckService _spellcheckService;
    private readonly IOcrModuleErrorAnalysisAggregator _errorAnalysisAggregator;
    private readonly IOcrModuleRegionCorrectionService _regionCorrectionService;

    public OcrModuleAnalysisController(
        IOcrModuleRepository repository,
        IOcrModuleSealSignatureService sealSignatureService,
        IOcrModuleSpellcheckService spellcheckService,
        IOcrModuleErrorAnalysisAggregator errorAnalysisAggregator,
        IOcrModuleRegionCorrectionService regionCorrectionService)
    {
        _repository = repository;
        _sealSignatureService = sealSignatureService;
        _spellcheckService = spellcheckService;
        _errorAnalysisAggregator = errorAnalysisAggregator;
        _regionCorrectionService = regionCorrectionService;
    }

    /// <summary>Yêu cầu 93 — phân loại Printed/Handwritten/Mixed theo từng vùng văn bản.</summary>
    [HttpPost("script-type/classify")]
    public async Task<ActionResult<ScriptTypeClassifyResponse>> ClassifyScriptType(string jobId, [FromQuery] int? pageNumber)
    {
        var regions = await _repository.GetAllRegionEntitiesAsync(jobId);
        if (pageNumber.HasValue)
        {
            regions = regions.Where(r => r.PageNumber == pageNumber.Value).ToList();
        }
        if (regions.Count == 0)
        {
            var message = pageNumber.HasValue
                ? $"Trang {pageNumber} chưa có vùng văn bản nào để phân loại."
                : "Job này chưa có vùng văn bản nào để phân loại.";
            return NotFound(new { code = "ERR_OCR_MODULE_NO_REGIONS", message });
        }

        var classification = ScriptTypeClassifier.ClassifyRegions(regions);
        await _repository.UpdateRegionScriptTypesAsync(classification);

        var summary = classification.Values
            .GroupBy(v => v)
            .ToDictionary(g => g.Key, g => g.Count());

        return Ok(new ScriptTypeClassifyResponse
        {
            TotalRegions = regions.Count,
            PrintedCount = summary.GetValueOrDefault("Printed"),
            HandwrittenCount = summary.GetValueOrDefault("Handwritten"),
            MixedCount = summary.GetValueOrDefault("Mixed"),
        });
    }

    /// <summary>
    /// Yêu cầu 88 — nhận diện + chuẩn hóa vùng văn bản giống công thức kỹ thuật, dựa trên text OCR
    /// đã có sẵn (không gọi lại ocr_vl_server, không dùng model OCR công thức chuyên dụng).
    /// </summary>
    [HttpPost("formula/run")]
    public async Task<ActionResult<FormulaRunResponse>> RunFormulaRecognition(string jobId, [FromQuery] int? pageNumber)
    {
        var regions = await _repository.GetAllRegionEntitiesAsync(jobId);
        if (pageNumber.HasValue)
        {
            regions = regions.Where(r => r.PageNumber == pageNumber.Value).ToList();
        }
        if (regions.Count == 0)
        {
            var message = pageNumber.HasValue
                ? $"Trang {pageNumber} chưa có vùng văn bản nào để nhận dạng công thức."
                : "Job này chưa có vùng văn bản nào để nhận dạng công thức.";
            return NotFound(new { code = "ERR_OCR_MODULE_NO_REGIONS", message });
        }

        var formulaRegions = new Dictionary<string, string>();
        foreach (var region in regions)
        {
            if (FormulaTextNormalizer.LooksLikeFormula(region.TextRaw))
            {
                formulaRegions[region.Id] = FormulaTextNormalizer.Normalize(region.TextRaw);
            }
        }

        // Trả về Text các vùng đã từng được gắn Formula ở lần chạy trước nhưng lần này không còn khớp
        // tiêu chí (vd. logic vừa được sửa để loại trừ ngày/số trang) — tránh nhãn Formula cũ bị kẹt lại.
        var noLongerFormulaIds = regions
            .Where(r => r.RegionType == "Formula" && !formulaRegions.ContainsKey(r.Id))
            .Select(r => r.Id)
            .ToList();
        await _repository.ResetFormulaRegionsAsync(noLongerFormulaIds);

        await _repository.UpdateRegionFormulasAsync(formulaRegions);

        return Ok(new FormulaRunResponse
        {
            TotalRegions = regions.Count,
            FormulaRegionCount = formulaRegions.Count,
        });
    }

    /// <summary>
    /// Yêu cầu 94 — tách vùng con dấu (nhận diện màu mực đỏ trên ảnh dựng lại từ PDF gốc) và gợi ý
    /// vùng chữ ký (heuristic vị trí + độ tin cậy OCR trên vùng văn bản đã có) — không dùng model AI.
    /// </summary>
    [HttpPost("seal-signature/run")]
    public async Task<ActionResult<SealSignatureRunResult>> RunSealSignatureDetection(string jobId, [FromQuery] int? pageNumber)
    {
        var job = await _repository.GetJobByIdAsync(jobId);
        if (job == null)
        {
            return NotFound(new { code = "ERR_OCR_MODULE_JOB_NOT_FOUND", message = "Không tìm thấy Job." });
        }

        try
        {
            var result = await _sealSignatureService.RunAsync(job, pageNumber);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = "ERR_OCR_MODULE_SEAL_SIGNATURE_FAILED", message = "Không thể xử lý xác thực con dấu/chữ ký.", details = new[] { ex.Message } });
        }
    }

    /// <summary>Yêu cầu 90 — so sánh layout Job hiện tại với 1 mẫu tham chiếu đã lưu.</summary>
    [HttpPost("template-diff/run")]
    public async Task<ActionResult<TemplateDiffRunResponse>> RunTemplateDiff(string jobId, [FromBody] RunTemplateDiffRequest request)
    {
        var template = await _repository.GetTemplateSnapshotByIdAsync(request.TemplateSnapshotId);
        if (template == null)
        {
            return NotFound(new { code = "ERR_OCR_MODULE_TEMPLATE_NOT_FOUND", message = "Không tìm thấy mẫu tham chiếu." });
        }

        var currentRegions = await _repository.GetAllRegionEntitiesAsync(jobId);
        var referenceRegions = JsonSerializer.Deserialize<List<TemplateRegionSnapshot>>(template.ReferenceRegionsJson) ?? [];
        if (request.PageNumber.HasValue)
        {
            currentRegions = currentRegions.Where(r => r.PageNumber == request.PageNumber.Value).ToList();
            referenceRegions = referenceRegions.Where(r => r.PageNumber == request.PageNumber.Value).ToList();
        }
        if (currentRegions.Count == 0)
        {
            var message = request.PageNumber.HasValue
                ? $"Trang {request.PageNumber} chưa có vùng văn bản nào để so sánh."
                : "Job này chưa có vùng văn bản nào để so sánh.";
            return NotFound(new { code = "ERR_OCR_MODULE_NO_REGIONS", message });
        }

        var diffs = TemplateDiffService.ComputeDiff(currentRegions, referenceRegions);

        foreach (var diff in diffs)
        {
            diff.Id = UuidHelper.NewUuid();
            diff.JobId = jobId;
            diff.TemplateSnapshotId = template.Id;
        }

        await _repository.InsertTemplateDiffResultsAsync(diffs);

        return Ok(new TemplateDiffRunResponse
        {
            TotalDiffs = diffs.Count,
            MissingCount = diffs.Count(d => d.DiffType == "Missing"),
            ExtraCount = diffs.Count(d => d.DiffType == "Extra"),
            TextMismatchCount = diffs.Count(d => d.DiffType == "TextMismatch"),
            PositionShiftCount = diffs.Count(d => d.DiffType == "PositionShift"),
        });
    }

    [HttpGet("template-diff/results")]
    public async Task<ActionResult<List<OcrModuleTemplateDiffResult>>> GetTemplateDiffResults(string jobId)
    {
        var results = await _repository.GetTemplateDiffResultsAsync(jobId);
        return Ok(results);
    }

    /// <summary>
    /// Yêu cầu 95 — kiểm tra chính tả các vùng văn bản của Job, dùng lại LLM server hiện có với prompt riêng.
    /// </summary>
    [HttpPost("spellcheck/run")]
    public async Task<ActionResult<SpellcheckRunResult>> RunSpellcheck(string jobId, [FromQuery] int? pageNumber)
    {
        try
        {
            var result = await _spellcheckService.RunAsync(jobId, pageNumber);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = "ERR_OCR_MODULE_SPELLCHECK_FAILED", message = "Không kiểm tra được chính tả.", details = new[] { ex.Message } });
        }
    }

    /// <summary>Chấp nhận/từ chối gợi ý chính tả, hoặc sửa tay trực tiếp.</summary>
    [HttpPut("regions/{regionId}/spellcheck")]
    public async Task<IActionResult> UpdateSpellcheckStatus(string jobId, string regionId, [FromBody] UpdateSpellcheckRequest request)
    {
        string? textRawOverride = request.Status switch
        {
            "Accepted" => request.SuggestionText,
            "ManuallyEdited" => request.ManualText,
            _ => null,
        };

        // "Rejected" không đổi TextRaw — chỉ cập nhật cột trạng thái, không cần đồng bộ MinIO/PDF/ES.
        if (textRawOverride == null)
        {
            await _repository.UpdateRegionSpellcheckStatusAsync(regionId, request.Status, textRawOverride: null);
            return Ok();
        }

        try
        {
            var updated = await _regionCorrectionService.ApplyManualCorrectionAsync(
                jobId, regionId, textRawOverride, request.Status, User?.Identity?.Name);
            return Ok(updated);
        }
        catch (RegionSourceIndexMissingException)
        {
            return Conflict(new { code = "ERR_OCR_MODULE_REGION_NOT_PATCHABLE",
                message = "Vùng này được tạo trước khi hỗ trợ sửa trực tiếp — không thể ghi lại vào file gốc." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "ERR_OCR_MODULE_REGION_NOT_FOUND", message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = "ERR_OCR_MODULE_REGION_EDIT_FAILED",
                message = "Không lưu được nội dung đã sửa.", details = new[] { ex.Message } });
        }
    }

    /// <summary>Sửa tay nội dung 1 box bất kỳ (tab "Kiểm tra chính tả và hiệu chỉnh nội dung") — đồng bộ
    /// lại MinIO/PDF 2 lớp/Elasticsearch, không gắn với ngữ cảnh gợi ý chính tả nào.</summary>
    [HttpPut("regions/{regionId}/text")]
    public async Task<ActionResult<OcrModuleRegionDto>> UpdateRegionText(string jobId, string regionId, [FromBody] UpdateRegionTextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TextRaw))
            return BadRequest(new { code = "ERR_OCR_MODULE_EMPTY_TEXT", message = "Nội dung không được để trống." });

        try
        {
            var updated = await _regionCorrectionService.ApplyManualCorrectionAsync(
                jobId, regionId, request.TextRaw, spellcheckStatus: null, editedBy: User?.Identity?.Name);
            return Ok(updated);
        }
        catch (RegionSourceIndexMissingException)
        {
            return Conflict(new { code = "ERR_OCR_MODULE_REGION_NOT_PATCHABLE",
                message = "Vùng này được tạo trước khi hỗ trợ sửa trực tiếp — không thể ghi lại vào file gốc." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "ERR_OCR_MODULE_REGION_NOT_FOUND", message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = "ERR_OCR_MODULE_REGION_EDIT_FAILED",
                message = "Không lưu được nội dung đã sửa.", details = new[] { ex.Message } });
        }
    }

    /// <summary>
    /// Yêu cầu 92 — tổng hợp lỗi có cấu trúc từ tín hiệu của các bước 88/90/93/94/95 đã chạy trên Job này.
    /// </summary>
    [HttpPost("error-analysis/run")]
    public async Task<ActionResult<List<OcrModuleErrorAnalysis>>> RunErrorAnalysis(string jobId, [FromQuery] int? pageNumber)
    {
        var errors = await _errorAnalysisAggregator.AggregateAsync(jobId, pageNumber);
        return Ok(errors);
    }

    [HttpGet("error-analysis")]
    public async Task<ActionResult<List<OcrModuleErrorAnalysis>>> GetErrorAnalysis(string jobId)
    {
        var errors = await _repository.GetErrorAnalysisAsync(jobId);
        return Ok(errors);
    }

    [HttpPut("error-analysis/{errorId}/resolve")]
    public async Task<IActionResult> ResolveErrorAnalysis(string jobId, string errorId)
    {
        await _repository.UpdateErrorAnalysisResolvedStatusAsync(errorId, "Resolved");
        return Ok();
    }
}

public class UpdateSpellcheckRequest
{
    /// <summary>Accepted | Rejected | ManuallyEdited</summary>
    public string Status { get; set; } = string.Empty;
    public string? SuggestionText { get; set; }
    public string? ManualText { get; set; }
}

public class UpdateRegionTextRequest
{
    public string TextRaw { get; set; } = string.Empty;
}

public class RunTemplateDiffRequest
{
    public string TemplateSnapshotId { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
}

public class TemplateDiffRunResponse
{
    public int TotalDiffs { get; set; }
    public int MissingCount { get; set; }
    public int ExtraCount { get; set; }
    public int TextMismatchCount { get; set; }
    public int PositionShiftCount { get; set; }
}

public class ScriptTypeClassifyResponse
{
    public int TotalRegions { get; set; }
    public int PrintedCount { get; set; }
    public int HandwrittenCount { get; set; }
    public int MixedCount { get; set; }
}

public class FormulaRunResponse
{
    public int TotalRegions { get; set; }
    public int FormulaRegionCount { get; set; }
}
