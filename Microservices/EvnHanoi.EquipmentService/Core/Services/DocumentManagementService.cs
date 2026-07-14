using System.IO.Compression;
using System.Text;
using System.Text.Json;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Core.Services;

/// <summary>
/// Service quản lý kho tài liệu thiết bị — CRUD thư mục và tài liệu
/// </summary>
public interface IDocumentManagementService
{
    // Folder operations
    Task<IEnumerable<FolderNodeDto>> GetFolderTreeByUnitAsync(long unitId);
    Task<FolderNodeDto?> GetFolderByIdAsync(Guid id);
    Task<Guid> CreateFolderAsync(CreateFolderDto dto, long unitId, string createdBy);
    Task<bool> UpdateFolderAsync(Guid id, UpdateFolderDto dto, string modifiedBy);
    Task<bool> DeleteFolderAsync(Guid id, string modifiedBy);
    Task<(byte[] ZipBytes, string FileName)?> DownloadFolderAsZipAsync(Guid folderId, long userUnitId);

    // Document operations
    Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsByFolderAsync(Guid? folderId, DocumentFilterDto filter);
    Task<DocumentListItemDto?> GetDocumentByIdAsync(Guid id);
    Task<Guid> CreateDocumentAsync(CreateDocumentDto dto, string createdBy);
    Task<bool> UpdateDocumentAsync(Guid id, UpdateDocumentDto dto, string modifiedBy);
    Task<bool> DeleteDocumentAsync(Guid id, string modifiedBy);

    // Document Version operations
    Task<IEnumerable<DocumentVersionDto>> GetDocumentVersionsAsync(Guid documentId);
    Task<bool> RollbackDocumentVersionAsync(Guid versionId, string userId);
    Task<bool> DeleteDocumentVersionAsync(Guid versionId, string userId);

    // Dossier Catalog tree operations
    Task<IEnumerable<FolderCatalogNodeDto>> GetDossierCatalogTreeAsync(long unitId);
    Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDossierCatalogDocumentsAsync(
        long unitId, 
        string? folderId, 
        string? keyword, 
        int page, 
        int pageSize);
}

public class DocumentManagementService : IDocumentManagementService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<DocumentManagementService> _logger;

    public DocumentManagementService(
        IDocumentRepository documentRepository,
        IFileStorageService fileStorageService,
        ILogger<DocumentManagementService> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ===== FOLDER OPERATIONS =====

    public async Task<IEnumerable<FolderNodeDto>> GetFolderTreeByUnitAsync(long unitId)
    {
        return await _documentRepository.GetFolderTreeByUnitAsync(unitId);
    }

    public async Task<FolderNodeDto?> GetFolderByIdAsync(Guid id)
    {
        return await _documentRepository.GetFolderByIdAsync(id);
    }

    public async Task<Guid> CreateFolderAsync(CreateFolderDto dto, long unitId, string createdBy)
    {
        // Validate folder name
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Tên thư mục không được để trống", nameof(dto.Name));

        dto.Name = dto.Name.Trim();

        if (dto.Name.Length > 255)
            throw new ArgumentException("Tên thư mục tối đa 255 ký tự", nameof(dto.Name));

        // Verify parent folder exists (if provided)
        if (dto.ParentId.HasValue)
        {
            var parentFolder = await _documentRepository.GetFolderByIdAsync(dto.ParentId.Value);
            if (parentFolder == null)
                throw new InvalidOperationException("Thư mục cha không tồn tại");
        }

        var folder = new Folder
        {
            Name = dto.Name,
            ParentId = dto.ParentId,
            UnitId = unitId,
            CreatedBy = createdBy
        };

        return await _documentRepository.CreateFolderAsync(folder);
    }

    public async Task<bool> UpdateFolderAsync(Guid id, UpdateFolderDto dto, string modifiedBy)
    {
        // Validate folder name
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Tên thư mục không được để trống", nameof(dto.Name));

        dto.Name = dto.Name.Trim();

        if (dto.Name.Length > 255)
            throw new ArgumentException("Tên thư mục tối đa 255 ký tự", nameof(dto.Name));

        var folder = new Folder
        {
            Id = id,
            Name = dto.Name,
            RowVersion = dto.RowVersion,
            ModifiedBy = modifiedBy
        };

        return await _documentRepository.UpdateFolderAsync(folder);
    }

    public async Task<bool> DeleteFolderAsync(Guid id, string modifiedBy)
    {
        return await _documentRepository.DeleteFolderAsync(id, modifiedBy);
    }

    public async Task<(byte[] ZipBytes, string FileName)?> DownloadFolderAsZipAsync(Guid folderId, long userUnitId)
    {
        var folder = await _documentRepository.GetFolderByIdAsync(folderId);
        if (folder == null)
            return null;

        if (userUnitId < folder.UnitId)
            throw new UnauthorizedAccessException("Bạn không có quyền tải thư mục này");

        using var memoryStream = new MemoryStream();
        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            await AddFolderToZipAsync(zipArchive, folderId, "");
        }

        memoryStream.Position = 0;
        var zipBytes = memoryStream.ToArray();
        var zipFileName = $"{SanitizeZipPathSegment(folder.Name)}.zip";
        return (zipBytes, zipFileName);
    }

    private async Task AddFolderToZipAsync(ZipArchive zipArchive, Guid folderId, string currentPath)
    {
        var documents = await _documentRepository.GetFolderDocumentsForZipAsync(folderId);
        var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in documents)
        {
            if (string.IsNullOrWhiteSpace(document.FilePath))
                continue;

            try
            {
                await using var fileStream = await _fileStorageService.DownloadFileAsync(document.FilePath);
                var entryName = BuildUniqueZipEntryName(currentPath, document.DocumentName, usedEntryNames);
                var zipEntry = zipArchive.CreateEntry(entryName, CompressionLevel.Optimal);
                await using var entryStream = zipEntry.Open();
                await fileStream.CopyToAsync(entryStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add document {DocumentName} to zip archive", document.DocumentName);
            }
        }

        var subfolders = await _documentRepository.GetChildFoldersByParentAsync(folderId);
        foreach (var subfolder in subfolders)
        {
            var segment = SanitizeZipPathSegment(subfolder.Name);
            var nextPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";
            await AddFolderToZipAsync(zipArchive, subfolder.Id, nextPath);
        }
    }

    private static string BuildUniqueZipEntryName(string currentPath, string documentName, HashSet<string> usedEntryNames)
    {
        var baseName = SanitizeZipPathSegment(documentName);
        var entryName = string.IsNullOrEmpty(currentPath) ? baseName : $"{currentPath}/{baseName}";

        if (usedEntryNames.Add(entryName))
            return entryName;

        var extension = Path.GetExtension(baseName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(baseName);
        var counter = 1;
        string candidate;
        do
        {
            var suffix = string.IsNullOrEmpty(extension)
                ? $"{nameWithoutExt}_{counter}"
                : $"{nameWithoutExt}_{counter}{extension}";
            candidate = string.IsNullOrEmpty(currentPath) ? suffix : $"{currentPath}/{suffix}";
            counter++;
        } while (!usedEntryNames.Add(candidate));

        return candidate;
    }

    private static string SanitizeZipPathSegment(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return "unnamed";

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        var result = builder.ToString().Trim();
        return string.IsNullOrEmpty(result) ? "unnamed" : result;
    }

    // ===== DOCUMENT OPERATIONS =====

    public async Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsByFolderAsync(Guid? folderId, DocumentFilterDto filter)
    {
        return await _documentRepository.GetDocumentsByFolderAsync(folderId, filter);
    }

    public async Task<DocumentListItemDto?> GetDocumentByIdAsync(Guid id)
    {
        return await _documentRepository.GetDocumentByIdAsync(id);
    }

    public async Task<Guid> CreateDocumentAsync(CreateDocumentDto dto, string createdBy)
    {
        // Validate document name
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Tên tài liệu không được để trống", nameof(dto.Name));

        dto.Name = dto.Name.Trim();

        if (dto.Name.Length > 255)
            throw new ArgumentException("Tên tài liệu tối đa 255 ký tự", nameof(dto.Name));

        // Verify folder exists (if provided)
        if (dto.FolderId.HasValue)
        {
            var folder = await _documentRepository.GetFolderByIdAsync(dto.FolderId.Value);
            if (folder == null)
                throw new InvalidOperationException("Thư mục không tồn tại");
        }

        var document = new Document
        {
            Name = dto.Name,
            FolderId = dto.FolderId,
            DossierId = dto.DossierId,
            CreatedBy = createdBy
        };

        return await _documentRepository.CreateDocumentAsync(document);
    }

    public async Task<bool> UpdateDocumentAsync(Guid id, UpdateDocumentDto dto, string modifiedBy)
    {
        // Validate document name
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Tên tài liệu không được để trống", nameof(dto.Name));

        dto.Name = dto.Name.Trim();

        if (dto.Name.Length > 255)
            throw new ArgumentException("Tên tài liệu tối đa 255 ký tự", nameof(dto.Name));

        var document = new Document
        {
            Id = id,
            Name = dto.Name,
            RowVersion = dto.RowVersion,
            ModifiedBy = modifiedBy
        };

        return await _documentRepository.UpdateDocumentAsync(document);
    }

    public async Task<bool> DeleteDocumentAsync(Guid id, string modifiedBy)
    {
        var document = await _documentRepository.GetDocumentByIdAsync(id);
        if (document == null)
            return false;

        var versions = (await _documentRepository.GetDocumentVersionsAsync(id)).ToList();
        foreach (var version in versions)
        {
            if (!string.IsNullOrEmpty(version.FilePath))
            {
                await _fileStorageService.DeleteFileAsync(
                    version.FilePath,
                    null,
                    version.MinioVersionId);
            }
        }

        await _documentRepository.SoftDeleteDocumentVersionsAsync(id, modifiedBy);
        return await _documentRepository.DeleteDocumentAsync(id, modifiedBy);
    }

    // ===== DOCUMENT VERSION OPERATIONS =====

    public async Task<IEnumerable<DocumentVersionDto>> GetDocumentVersionsAsync(Guid documentId)
    {
        return await _documentRepository.GetDocumentVersionsAsync(documentId);
    }

    public async Task<bool> RollbackDocumentVersionAsync(Guid versionId, string userId)
    {
        var targetVersion = await _documentRepository.GetDocumentVersionByIdAsync(versionId);
        if (targetVersion == null || string.IsNullOrEmpty(targetVersion.FilePath))
        {
            _logger.LogWarning("Rollback target version {VersionId} not found or has no file.", versionId);
            return false;
        }

        var document = await _documentRepository.GetDocumentByIdAsync(targetVersion.DocumentId);
        if (document == null)
        {
            _logger.LogWarning("Document {DocumentId} for target version not found.", targetVersion.DocumentId);
            return false;
        }

        if (!document.FolderId.HasValue)
        {
            _logger.LogWarning("Document {DocumentId} is not in a folder, rollback currently only supported for folder files.", document.Id);
            return false;
        }

        var folder = await _documentRepository.GetFolderByIdAsync(document.FolderId.Value);
        var unitCode = folder?.UnitCode ?? "system";

        // Download the target file version
        using var stream = await _fileStorageService.DownloadFileAsync(
            targetVersion.FilePath,
            null,
            targetVersion.MinioVersionId);

        // Upload it back to MinIO as a new version
        var (newPath, newMinioVersionId) = await _fileStorageService.UploadFileAsync(
            stream,
            document.Name ?? "rollback_file",
            targetVersion.MimeType ?? "application/octet-stream",
            targetVersion.FileSize,
            unitCode,
            document.FolderId.Value);

        // Determine new version number
        var versions = await _documentRepository.GetDocumentVersionsAsync(document.Id);
        var maxVersion = versions.Any() ? versions.Max(v => v.VersionNumber) : 0;

        var newVersion = new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = maxVersion + 1,
            UploadSource = 3, // Web/Rollback
            FilePath = newPath,
            MinioVersionId = newMinioVersionId,
            FileSize = targetVersion.FileSize,
            MimeType = targetVersion.MimeType,
            CreatedBy = userId
        };

        var newVersionId = await _documentRepository.CreateDocumentVersionAsync(newVersion);
        _logger.LogInformation("Successfully rolled back document {DocumentId} to version {VersionNumber} (New VersionId: {NewVersionId})",
            document.Id, targetVersion.VersionNumber, newVersionId);

        return true;
    }

    public async Task<bool> DeleteDocumentVersionAsync(Guid versionId, string userId)
    {
        var version = await _documentRepository.GetDocumentVersionByIdAsync(versionId);
        if (version == null)
        {
            _logger.LogWarning("Document version {VersionId} not found.", versionId);
            return false;
        }

        if (!string.IsNullOrEmpty(version.FilePath))
        {
            await _fileStorageService.DeleteFileAsync(
                version.FilePath,
                null,
                version.MinioVersionId);
        }

        return await _documentRepository.SoftDeleteDocumentVersionAsync(versionId, userId);
    }

    // ===== DOSSIER CATALOG TREE OPERATIONS =====

    private string GetDossierDisplayName(string? formDataJson, string dossierTypeName, string dossierSetName, string dossierId)
    {
        string? name = null;
        string? code = null;

        if (!string.IsNullOrEmpty(formDataJson))
        {
            try
            {
                using (var doc = JsonDocument.Parse(formDataJson))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (root.TryGetProperty("NAME", out var pName)) name = pName.GetString();
                        else if (root.TryGetProperty("name", out pName)) name = pName.GetString();
                        else if (root.TryGetProperty("Dossier_Name", out pName)) name = pName.GetString();
                        else if (root.TryGetProperty("dossier_name", out pName)) name = pName.GetString();

                        if (root.TryGetProperty("CODE", out var pCode)) code = pCode.GetString();
                        else if (root.TryGetProperty("code", out pCode)) code = pCode.GetString();
                        else if (root.TryGetProperty("Dossier_Code", out pCode)) code = pCode.GetString();
                        else if (root.TryGetProperty("dossier_code", out pCode)) code = pCode.GetString();
                    }
                }
            }
            catch
            {
                // Ignore JSON parsing errors
            }
        }

        name = name?.Trim();
        code = code?.Trim();

        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(code))
        {
            return $"{name} ({code})";
        }
        else if (!string.IsNullOrEmpty(name))
        {
            return name;
        }
        else if (!string.IsNullOrEmpty(code))
        {
            return $"{dossierTypeName} - {code}";
        }

        string suffix = dossierId.Length >= 8 ? dossierId.Substring(0, 8) : dossierId;
        return $"{dossierTypeName} ({suffix})";
    }

    public async Task<IEnumerable<FolderCatalogNodeDto>> GetDossierCatalogTreeAsync(long unitId)
    {
        // 1. Get Unit Info
        var unitInfo = await _documentRepository.GetUnitInfoAsync(unitId);
        if (unitInfo == null || string.IsNullOrEmpty(unitInfo.Name))
        {
            throw new InvalidOperationException("Không tìm thấy thông tin đơn vị trong hệ thống");
        }

        // 2. Query Infrastructures (both Substations (1) and Power lines (2))
        var infrastructures = await _documentRepository.GetActiveInfrastructuresByUnitAsync(unitId);

        // 3. Query active dossiers for this unit
        var dossiers = await _documentRepository.GetActiveDossiersByUnitAsync(unitId);

        var nodes = new List<FolderCatalogNodeDto>();
        var unitCode = unitInfo.Code ?? string.Empty;

        // Tập hợp các ID của các node lưới điện và root cần thêm
        var requiredParentIds = new HashSet<string>();

        // Tách biệt Substations (type = 1) và Power Lines (type = 2)
        var substations = infrastructures.Where(x => x.InfraTypeId == 1);
        var powerLines = infrastructures.Where(x => x.InfraTypeId == 2);

        // 1. Mapping Trạm biến áp (4 cấp)
        foreach (var sub in substations)
        {
            bool isHighVoltageSub = sub.GridTypeId == 1 || 
                                    (sub.GridTypeId == null && (
                                        (sub.Name != null && (sub.Name.Contains("110") || sub.Name.Contains("220") || sub.Name.Contains("500")))
                                        || (sub.Code != null && (sub.Code.Contains("110") || sub.Code.Contains("220") || sub.Code.Contains("500")))
                                    ));

            string parentId = isHighVoltageSub ? "tba-cao-ap" : "tba-trung-ap";
            string subNodeId = isHighVoltageSub ? $"tba-cao-ap_{sub.Id}" : $"tba-trung-ap_{sub.Id}";

            // Lọc các dossiers thuộc trạm này
            var subDossiers = dossiers.Where(d => string.Equals(d.InfrastructureId, sub.Id, StringComparison.OrdinalIgnoreCase));

            var dossierGroups = subDossiers
                .GroupBy(d => new { d.DossierTypeId, d.DossierTypeName })
                .OrderBy(g => g.Key.DossierTypeName)
                .ToList();

            if (dossierGroups.Any())
            {
                // Thêm node trạm (cấp 3)
                nodes.Add(new FolderCatalogNodeDto
                {
                    Id = subNodeId,
                    Name = $"{sub.Name} ({sub.Code})",
                    ParentId = parentId,
                    UnitId = unitId,
                    UnitCode = unitCode,
                    CreatedDate = DateTime.UtcNow
                });

                // Thêm các node hộp con (cấp 4)
                foreach (var g in dossierGroups)
                {
                    if (string.IsNullOrEmpty(g.Key.DossierTypeId)) continue;
                    nodes.Add(new FolderCatalogNodeDto
                    {
                        Id = $"type_{subNodeId}_{g.Key.DossierTypeId}",
                        Name = $"{g.Key.DossierTypeName} ({g.Count()})", // Số lượng hồ sơ đã xuất bản
                        ParentId = subNodeId,
                        UnitId = unitId,
                        UnitCode = unitCode,
                        CreatedDate = DateTime.UtcNow
                    });
                }

                // Ghi nhận lưới điện cha và root cha cần hiển thị
                requiredParentIds.Add(parentId);
            }
        }

        // 2. Mapping Đường dây (4 cấp)
        foreach (var infra in powerLines)
        {
            bool isHighVoltageInfra = infra.GridTypeId == 1 || 
                                      (infra.GridTypeId == null && (
                                          (infra.Name != null && (infra.Name.Contains("110") || infra.Name.Contains("220") || infra.Name.Contains("500")))
                                          || (infra.Code != null && (infra.Code.Contains("110") || infra.Code.Contains("220") || infra.Code.Contains("500")))
                                      ));

            string parentId = isHighVoltageInfra ? "dd-cao-ap" : "dd-trung-ap";
            string lineNodeId = isHighVoltageInfra ? $"dd-cao-ap_{infra.Id}" : $"dd-trung-ap_{infra.Id}";

            // Lọc các dossiers thuộc đường dây này
            var lineDossiers = dossiers.Where(d => string.Equals(d.InfrastructureId, infra.Id, StringComparison.OrdinalIgnoreCase));

            var dossierGroups = lineDossiers
                .GroupBy(d => new { d.DossierTypeId, d.DossierTypeName })
                .OrderBy(g => g.Key.DossierTypeName)
                .ToList();

            if (dossierGroups.Any())
            {
                // Thêm node đường dây (cấp 3)
                nodes.Add(new FolderCatalogNodeDto
                {
                    Id = lineNodeId,
                    Name = $"{infra.Name} ({infra.Code})",
                    ParentId = parentId,
                    UnitId = unitId,
                    UnitCode = unitCode,
                    CreatedDate = DateTime.UtcNow
                });

                // Thêm các node hộp con (cấp 4)
                foreach (var g in dossierGroups)
                {
                    if (string.IsNullOrEmpty(g.Key.DossierTypeId)) continue;
                    nodes.Add(new FolderCatalogNodeDto
                    {
                        Id = $"type_{lineNodeId}_{g.Key.DossierTypeId}",
                        Name = $"{g.Key.DossierTypeName} ({g.Count()})", // Số lượng hồ sơ đã xuất bản
                        ParentId = lineNodeId,
                        UnitId = unitId,
                        UnitCode = unitCode,
                        CreatedDate = DateTime.UtcNow
                    });
                }

                // Ghi nhận lưới điện cha và root cha cần hiển thị
                requiredParentIds.Add(parentId);
            }
        }

        // 3. Chỉ thêm các node lưới điện (cấp 2) và root (cấp 1) cần thiết
        if (requiredParentIds.Contains("tba-cao-ap") || requiredParentIds.Contains("tba-trung-ap"))
        {
            nodes.Add(new FolderCatalogNodeDto
            {
                Id = "root-tba",
                Name = "Trạm biến áp",
                ParentId = null,
                UnitId = unitId,
                UnitCode = unitCode,
                CreatedDate = DateTime.UtcNow
            });

            if (requiredParentIds.Contains("tba-cao-ap"))
            {
                nodes.Add(new FolderCatalogNodeDto
                {
                    Id = "tba-cao-ap",
                    Name = "Lưới điện cao áp",
                    ParentId = "root-tba",
                    UnitId = unitId,
                    UnitCode = unitCode,
                    CreatedDate = DateTime.UtcNow
                });
            }

            if (requiredParentIds.Contains("tba-trung-ap"))
            {
                nodes.Add(new FolderCatalogNodeDto
                {
                    Id = "tba-trung-ap",
                    Name = "Lưới điện trung áp",
                    ParentId = "root-tba",
                    UnitId = unitId,
                    UnitCode = unitCode,
                    CreatedDate = DateTime.UtcNow
                });
            }
        }

        if (requiredParentIds.Contains("dd-cao-ap") || requiredParentIds.Contains("dd-trung-ap"))
        {
            nodes.Add(new FolderCatalogNodeDto
            {
                Id = "root-dd",
                Name = "Đường dây",
                ParentId = null,
                UnitId = unitId,
                UnitCode = unitCode,
                CreatedDate = DateTime.UtcNow
            });

            if (requiredParentIds.Contains("dd-cao-ap"))
            {
                nodes.Add(new FolderCatalogNodeDto
                {
                    Id = "dd-cao-ap",
                    Name = "Lưới điện cao áp",
                    ParentId = "root-dd",
                    UnitId = unitId,
                    UnitCode = unitCode,
                    CreatedDate = DateTime.UtcNow
                });
            }

            if (requiredParentIds.Contains("dd-trung-ap"))
            {
                nodes.Add(new FolderCatalogNodeDto
                {
                    Id = "dd-trung-ap",
                    Name = "Lưới điện trung áp",
                    ParentId = "root-dd",
                    UnitId = unitId,
                    UnitCode = unitCode,
                    CreatedDate = DateTime.UtcNow
                });
            }
        }

        return nodes;
    }

    public async Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDossierCatalogDocumentsAsync(
        long unitId, 
        string? folderId, 
        string? keyword, 
        int page, 
        int pageSize)
    {
        if (string.IsNullOrWhiteSpace(folderId))
        {
            return (Enumerable.Empty<DocumentListItemDto>(), 0);
        }

        // Only Level 4 dossier nodes are selectable and return documents
        if (folderId.StartsWith("dossier_", StringComparison.OrdinalIgnoreCase))
        {
            var dossierIdStr = folderId.Substring("dossier_".Length);
            if (Guid.TryParse(dossierIdStr, out var dossierId))
            {
                var dossierFilter = new DossierDocumentFilterDto
                {
                    Keyword = keyword,
                    Page = page,
                    PageSize = pageSize
                };
                return await _documentRepository.GetDocumentsByDossierAsync(dossierId, dossierFilter);
            }
        }

        return (Enumerable.Empty<DocumentListItemDto>(), 0);
    }
}
