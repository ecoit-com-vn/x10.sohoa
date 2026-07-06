using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Security;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// API quản lý kho tài liệu thiết bị
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/documents")]
public partial class DocumentController : ControllerBase
{
    private readonly IDocumentManagementService _documentService;
    private readonly IFileUploadService _fileUploadService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(
        IDocumentManagementService documentService,
        IFileUploadService fileUploadService,
        ILogger<DocumentController> logger)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _fileUploadService = fileUploadService ?? throw new ArgumentNullException(nameof(fileUploadService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? User.Identity?.Name ?? "system";

    private string UserName => User.FindFirst("preferred_username")?.Value
                             ?? User.FindFirst(ClaimTypes.Name)?.Value
                             ?? User.Identity?.Name ?? "system";

    private long GetUserUnitId()
    {
        var unitIdClaim = User.FindFirst("unit_id")?.Value;
        return long.TryParse(unitIdClaim, out var unitId) ? unitId : 0;
    }

    // ===== FOLDER ENDPOINTS =====

    /// <summary>
    /// Lấy cây thư mục của unit hiện tại (dùng chọn tài liệu từ kho khi gắn vào hồ sơ).
    /// Bypass quyền DOCUMENT_* — vẫn giới hạn theo unit_id trong JWT.
    /// </summary>
    [HttpGet("folders/tree")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetFolderTree()
    {
        var unitId = GetUserUnitId();
        if (unitId == 0)
            return Unauthorized("Không thể xác định đơn vị của người dùng");

        var tree = await _documentService.GetFolderTreeByUnitAsync(unitId);
        return Ok(tree);
    }

    /// <summary>
    /// Lấy chi tiết một thư mục
    /// </summary>
    [HttpGet("folders/{id}")]
    public async Task<IActionResult> GetFolderDetail([FromRoute] Guid id)
    {
        var folder = await _documentService.GetFolderByIdAsync(id);
        if (folder == null)
            return NotFound();

        return Ok(folder);
    }

    /// <summary>
    /// Tạo thư mục mới
    /// </summary>
    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolder([FromBody] CreateFolderDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var unitId = GetUserUnitId();
            if (unitId == 0)
                return Unauthorized("Không thể xác định đơn vị của người dùng");

            var folderId = await _documentService.CreateFolderAsync(dto, unitId, UserName);
            return Created($"api/v1/documents/folders/{folderId}", new { id = folderId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_FOLDER", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "OPERATION_FAILED", message = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật thư mục
    /// </summary>
    [HttpPut("folders/{id}")]
    public async Task<IActionResult> UpdateFolder([FromRoute] Guid id, [FromBody] UpdateFolderDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _documentService.UpdateFolderAsync(id, dto, UserName);
            if (!result)
                return Conflict(new { code = "CONCURRENCY_CONFLICT", message = "Thư mục đã bị thay đổi bởi người dùng khác. Hãy tải lại dữ liệu." });

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_FOLDER", message = ex.Message });
        }
    }

    /// <summary>
    /// Xóa thư mục (soft delete)
    /// </summary>
    [HttpDelete("folders/{id}")]
    public async Task<IActionResult> DeleteFolder([FromRoute] Guid id)
    {
        var result = await _documentService.DeleteFolderAsync(id, UserName);
        if (!result)
            return NotFound();

        return NoContent();
    }

    // ===== DOCUMENT ENDPOINTS =====

    /// <summary>
    /// Lấy danh sách tài liệu trong một thư mục (có phân trang).
    /// Bypass quyền DOCUMENT_* khi duyệt kho để chọn tài liệu gắn hồ sơ.
    /// </summary>
    [HttpGet("list")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDocumentsList(
        [FromQuery] Guid? folderId,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var filter = new DocumentFilterDto
        {
            FolderId = folderId,
            Keyword = keyword,
            Page = page,
            PageSize = pageSize
        };

        var (items, totalCount) = await _documentService.GetDocumentsByFolderAsync(folderId, filter);
        return Ok(new { items, totalCount, page, pageSize });
    }

    /// <summary>
    /// Lấy chi tiết một tài liệu
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDocumentDetail([FromRoute] Guid id)
    {
        var document = await _documentService.GetDocumentByIdAsync(id);
        if (document == null)
            return NotFound();

        return Ok(document);
    }

    /// <summary>
    /// Tạo tài liệu mới
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateDocument([FromBody] CreateDocumentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var documentId = await _documentService.CreateDocumentAsync(dto, UserName);
            return Created($"api/v1/documents/{documentId}", new { id = documentId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_DOCUMENT", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "OPERATION_FAILED", message = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật tài liệu
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDocument([FromRoute] Guid id, [FromBody] UpdateDocumentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _documentService.UpdateDocumentAsync(id, dto, UserName);
            if (!result)
                return Conflict(new { code = "CONCURRENCY_CONFLICT", message = "Tài liệu đã bị thay đổi bởi người dùng khác. Hãy tải lại dữ liệu." });

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_DOCUMENT", message = ex.Message });
        }
    }

    /// <summary>
    /// Xóa tài liệu (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDocument([FromRoute] Guid id)
    {
        var result = await _documentService.DeleteDocumentAsync(id, UserName);
        if (!result)
            return NotFound();

        return NoContent();
    }

    // ===== DOCUMENT VERSION ENDPOINTS =====

    /// <summary>
    /// Lấy lịch sử phiên bản của một tài liệu
    /// </summary>
    [HttpGet("{documentId}/versions")]
    public async Task<IActionResult> GetDocumentVersionsList([FromRoute] Guid documentId)
    {
        var versions = await _documentService.GetDocumentVersionsAsync(documentId);
        return Ok(versions);
    }
}
