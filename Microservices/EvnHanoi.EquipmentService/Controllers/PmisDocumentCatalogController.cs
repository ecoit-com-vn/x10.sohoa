using System.Security.Claims;
using System.Text.Json;
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
///
/// Phân quyền theo đơn vị: quản trị hệ thống (role "ADMIN") xem được TẤT CẢ; người dùng khác chỉ xem
/// được dữ liệu thuộc đơn vị đăng nhập (kèm các đơn vị con, theo cây ORGANIZATION_UNIT) — cùng cơ chế
/// GetAllowedUnitIdsAsync đã dùng ở EquipmentController/SubstationController/TransmissionLineController.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/pmis-documents")]
public class PmisDocumentCatalogController : ControllerBase
{
    private readonly IPmisDocumentRepository _pmisDocumentRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IFileDownloadTokenService _downloadTokenService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IMimeTypeValidationService _mimeTypeValidationService;
    private readonly IClamAvService _antivirusService;

    public PmisDocumentCatalogController(
        IPmisDocumentRepository pmisDocumentRepository,
        IEquipmentRepository equipmentRepository,
        IFileDownloadTokenService downloadTokenService,
        IFileStorageService fileStorageService,
        IMimeTypeValidationService mimeTypeValidationService,
        IClamAvService antivirusService)
    {
        _pmisDocumentRepository = pmisDocumentRepository;
        _equipmentRepository = equipmentRepository;
        _downloadTokenService = downloadTokenService;
        _fileStorageService = fileStorageService;
        _mimeTypeValidationService = mimeTypeValidationService;
        _antivirusService = antivirusService;
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? "system";
    private string? UserFullName => User.FindFirst("full_name")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value;

    /// <summary>Cấp gốc của cây (chỉ danh sách Đơn vị) — FE chỉ gọi API này khi người dùng mở node gốc
    /// mặc định, không tải kèm Trạm/Đường dây/Thiết bị.</summary>
    [HttpGet("catalog/units")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetCatalogUnits()
    {
        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        var nodes = await _pmisDocumentRepository.GetCatalogUnitsAsync(allowedUnitIds);
        return Ok(nodes);
    }

    /// <summary>Trạm/Đường dây + Thiết bị của đúng 1 Đơn vị — FE chỉ gọi API này khi người dùng click mở
    /// 1 công ty cụ thể trong cây (load lười theo cấp, thay vì tải toàn bộ cây 1 lần).</summary>
    [HttpGet("catalog/units/{unitNodeId}/children")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetCatalogUnitChildren(string unitNodeId)
    {
        if (string.IsNullOrWhiteSpace(unitNodeId))
            return BadRequest(new { message = "Thiếu unitNodeId." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && !IsUnitNodeAllowed(unitNodeId, allowedUnitIds))
            return Forbid();

        var nodes = await _pmisDocumentRepository.GetCatalogUnitChildrenAsync(unitNodeId);
        return Ok(nodes);
    }

    /// <summary>Toàn bộ Trạm/Đường dây đã có tài liệu PMIS - dùng cho ô tìm kiếm phía trên cây, cho phép
    /// nhảy thẳng tới đúng Trạm/Đường dây mà không cần duyệt qua từng công ty.</summary>
    [HttpGet("catalog/infrastructures/lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> SearchInfrastructures()
    {
        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        var items = await _pmisDocumentRepository.SearchInfrastructuresAsync(allowedUnitIds);
        return Ok(items);
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

        if (!await IsOwnerAllowedAsync(ownerType, ownerId.Value))
            return Forbid();

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

        if (!await IsOwnerAllowedAsync(ownerType, ownerId.Value))
            return Forbid();

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

        if (!await IsOwnerAllowedAsync(doc.OwnerType, doc.OwnerId))
            return Forbid();

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

    /// <summary>"unit_{id}" nằm trong danh sách được phép, hoặc "unit_unassigned" — vì unassigned chỉ bao
    /// giờ được trả về bởi GetCatalogUnitsAsync khi allowedUnitIds null (quản trị hệ thống), nên nếu
    /// allowedUnitIds có giá trị (người dùng thường) mà cố tình gọi thẳng unitNodeId này thì luôn bị chặn.</summary>
    private static bool IsUnitNodeAllowed(string unitNodeId, List<long> allowedUnitIds)
    {
        if (string.Equals(unitNodeId, "unit_unassigned", StringComparison.OrdinalIgnoreCase))
            return false;

        var rawId = unitNodeId.StartsWith("unit_", StringComparison.OrdinalIgnoreCase) ? unitNodeId["unit_".Length..] : unitNodeId;
        return long.TryParse(rawId, out var unitId) && allowedUnitIds.Contains(unitId);
    }

    /// <summary>Kiểm tra Trạm/Đường dây/Thiết bị (ownerType/ownerId) có thuộc phạm vi đơn vị được phép của
    /// người dùng đăng nhập hay không — dùng ở mọi API đọc/ghi theo 1 node lá cụ thể (xem tài liệu, upload,
    /// tạo download token) để chặn truy cập trực tiếp bằng Id ngoài phạm vi hiển thị trên cây/bảng.</summary>
    private async Task<bool> IsOwnerAllowedAsync(string ownerType, Guid ownerId)
    {
        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds == null) return true; // quản trị hệ thống - không giới hạn

        var unitId = await _pmisDocumentRepository.GetOwnerUnitIdAsync(ownerType, ownerId);
        return unitId.HasValue && allowedUnitIds.Contains(unitId.Value);
    }

    /// <summary>null = quản trị hệ thống (role "ADMIN"), không giới hạn - xem tất cả. Có giá trị = danh
    /// sách Id đơn vị được phép (đơn vị đăng nhập + toàn bộ đơn vị con, theo cây ORGANIZATION_UNIT) - cùng
    /// cơ chế EquipmentController.GetAllowedUnitIdsAsync.</summary>
    private async Task<List<long>?> GetAllowedUnitIdsAsync()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        if (isAdmin)
        {
            return null;
        }

        var userUnitId = GetUserUnitIdFromClaims();
        if (userUnitId.HasValue)
        {
            var allowedUnits = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(userUnitId.Value);
            return allowedUnits.Select(u => u.Id).ToList();
        }

        var fallbackUnitIds = GetAuthorizedUnitIds();
        if (fallbackUnitIds != null && fallbackUnitIds.Any())
        {
            var list = new List<long>();
            foreach (var fId in fallbackUnitIds)
            {
                var units = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(fId);
                list.AddRange(units.Select(u => u.Id));
            }
            return list.Distinct().ToList();
        }

        return new List<long> { -1 };
    }

    private long? GetUserUnitIdFromClaims()
    {
        var claimNames = new[]
        {
            "unit_id",
            "UnitId",
            "unitId",
            "Unit_Id",
            "organization_unit_id",
            "organizationUnitId",
            "OrganizationUnitId"
        };

        foreach (var claimName in claimNames)
        {
            var value = User.FindFirst(claimName)?.Value;
            if (long.TryParse(value, out var unitId) && unitId > 0)
                return unitId;
        }

        return null;
    }

    private List<long>? GetAuthorizedUnitIds()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        if (isAdmin)
        {
            return null;
        }

        var unitRolesClaim = User.FindFirst("unit_roles")?.Value;
        if (string.IsNullOrEmpty(unitRolesClaim))
        {
            return new List<long>();
        }

        try
        {
            var unitRoles = JsonSerializer.Deserialize<List<UnitRoleDto>>(unitRolesClaim, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return unitRoles?.Select(ur => ur.UnitId).Distinct().ToList() ?? new List<long>();
        }
        catch
        {
            return new List<long>();
        }
    }

    public class UnitRoleDto
    {
        public long UnitId { get; set; }
        public long RoleId { get; set; }
        public string RoleCode { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }
}
