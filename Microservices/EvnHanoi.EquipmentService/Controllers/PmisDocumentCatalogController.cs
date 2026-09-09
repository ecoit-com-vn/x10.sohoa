using System.Security.Claims;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// "Kho tài liệu PMIS" — xem tài liệu đính kèm Trạm/Đường dây/Thiết bị đã đồng bộ từ PMIS
/// (PMIS_DOCUMENT, ghi bởi InternalPmisSyncController.UpsertDocumentsFromPmis). Cây Đơn vị → Trạm biến
/// áp/Đường dây → Thiết bị được TỔNG HỢP từ dữ liệu thật (ORGANIZATION_UNIT/INFRASTRUCTURE/EQUIPMENTS),
/// KHÔNG có bảng folder riêng — cùng tinh thần DossierCatalogController nhưng đơn giản hơn (3 cấp cố
/// định). Chỉ đọc — không có API tạo/sửa/xoá (dữ liệu đến từ đồng bộ PMIS).
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/pmis-documents")]
public class PmisDocumentCatalogController : ControllerBase
{
    private readonly IPmisDocumentRepository _pmisDocumentRepository;
    private readonly IFileDownloadTokenService _downloadTokenService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IMimeTypeValidationService _mimeTypeValidationService;
    private readonly IClamAvService _antivirusService;

    public PmisDocumentCatalogController(
        IPmisDocumentRepository pmisDocumentRepository,
        IFileDownloadTokenService downloadTokenService,
        IFileStorageService fileStorageService,
        IMimeTypeValidationService mimeTypeValidationService,
        IClamAvService antivirusService)
    {
        _pmisDocumentRepository = pmisDocumentRepository;
        _downloadTokenService = downloadTokenService;
        _fileStorageService = fileStorageService;
        _mimeTypeValidationService = mimeTypeValidationService;
        _antivirusService = antivirusService;
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? "system";
    private string? UserFullName => User.FindFirst("full_name")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value;

    [HttpGet("catalog/tree")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetCatalogTree()
    {
        var nodes = await _pmisDocumentRepository.GetCatalogTreeAsync();
        return Ok(nodes);
    }

    [HttpGet("catalog/documents")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetCatalogDocuments(
        [FromQuery] string folderId,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(folderId))
            return BadRequest(new { message = "Thiếu folderId." });

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var (ownerType, ownerId) = ResolveOwner(folderId);
        if (ownerType == null || ownerId == null)
            return Ok(new { items = Array.Empty<object>(), totalCount = 0, page, pageSize });

        var (items, totalCount) = await _pmisDocumentRepository.GetByOwnerAsync(ownerType, ownerId.Value, keyword, page, pageSize);
        return Ok(new { items, totalCount, page, pageSize });
    }

    /// <summary>Nút "Upload tài liệu" thủ công trên "Kho tài liệu PMIS" — dùng khi đồng bộ tự động từ
    /// PMIS lỗi (mạng, timeout, PMIS không có sẵn tài liệu...), cho phép người dùng tự bổ sung tài liệu
    /// trực tiếp vào đúng Trạm/Đường dây/Thiết bị. Kiểm tra mimeType + virus giống hệt luồng upload tài
    /// liệu hồ sơ (UploadFileToDossierDirectAsync) — tài liệu PMIS không có lý do gì được lỏng hơn.</summary>
    [HttpPost("catalog/{folderId}/upload")]
    [RequestSizeLimit(10_485_760)]
    [BypassDynamicPermission]
    public async Task<IActionResult> UploadDocument(
        string folderId,
        [FromForm] IFormFile file,
        [FromForm] string? documentName,
        [FromForm] string? documentType,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File không được để trống." });

        var (ownerType, ownerId) = ResolveOwner(folderId);
        if (ownerType == null || ownerId == null)
            return BadRequest(new { message = "Chỉ upload được vào đúng 1 Trạm biến áp, Đường dây hoặc Thiết bị cụ thể." });

        if (!await _mimeTypeValidationService.IsAllowedMimeTypeAsync(file.ContentType))
            return BadRequest(new { message = $"Loại file không được hỗ trợ: {file.ContentType}" });

        var finalName = string.IsNullOrWhiteSpace(documentName) ? file.FileName : documentName;

        await using var stream = file.OpenReadStream();

        var scanResult = await _antivirusService.ScanFileAsync(stream, file.FileName, cancellationToken);
        if (!scanResult.IsClean)
            return BadRequest(new { message = $"File bị phát hiện chứa mã độc: {scanResult.Threat}" });

        stream.Seek(0, SeekOrigin.Begin);
        var (objectKey, _) = await _fileStorageService.UploadPmisDocumentAsync(
            stream, finalName, file.ContentType, file.Length, ownerType, ownerId.Value, cancellationToken);

        var id = await _pmisDocumentRepository.InsertManualAsync(
            ownerType, ownerId.Value, finalName, documentType, objectKey, file.Length, UserFullName ?? UserId);

        return Ok(new { id, documentName = finalName });
    }

    /// <summary>Tạo download token 1 lần (giống hệt cơ chế FileDownloadTokenController dùng cho "Kho tài
    /// liệu thiết bị") — tránh lộ thẳng ObjectKey MinIO, không cần JWT ở bước tải file thật.</summary>
    [HttpGet("{id:guid}/download-url")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetDownloadToken(Guid id, CancellationToken cancellationToken)
    {
        var doc = await _pmisDocumentRepository.GetByIdAsync(id);
        if (doc == null)
            return NotFound(new { message = "Không tìm thấy tài liệu." });

        if (string.IsNullOrEmpty(doc.ObjectKey))
            return BadRequest(new { message = "Tài liệu chưa có file (đồng bộ PMIS chưa tải được file)." });

        var result = await _downloadTokenService.CreateTokenAsync(
            doc.ObjectKey,
            doc.DocumentName ?? doc.PmisDocumentCode,
            "application/octet-stream",
            bucketName: _fileStorageService.DocumentBucketName,
            cancellationToken: cancellationToken);

        return Ok(result);
    }

    private static (string? OwnerType, Guid? OwnerId) ResolveOwner(string folderId)
    {
        if (folderId.StartsWith("infra_", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(folderId["infra_".Length..], out var infraId))
        {
            return ("INFRASTRUCTURE", infraId);
        }

        if (folderId.StartsWith("equipment_", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(folderId["equipment_".Length..], out var equipmentId))
        {
            return ("EQUIPMENT", equipmentId);
        }

        return (null, null);
    }
}
