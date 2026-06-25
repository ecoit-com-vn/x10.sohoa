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

    // Document operations
    Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsByFolderAsync(Guid? folderId, DocumentFilterDto filter);
    Task<DocumentListItemDto?> GetDocumentByIdAsync(Guid id);
    Task<Guid> CreateDocumentAsync(CreateDocumentDto dto, string createdBy);
    Task<bool> UpdateDocumentAsync(Guid id, UpdateDocumentDto dto, string modifiedBy);
    Task<bool> DeleteDocumentAsync(Guid id, string modifiedBy);

    // Document Version operations
    Task<IEnumerable<DocumentVersionDto>> GetDocumentVersionsAsync(Guid documentId);

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

    public DocumentManagementService(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
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
        return await _documentRepository.DeleteDocumentAsync(id, modifiedBy);
    }

    // ===== DOCUMENT VERSION OPERATIONS =====

    public async Task<IEnumerable<DocumentVersionDto>> GetDocumentVersionsAsync(Guid documentId)
    {
        return await _documentRepository.GetDocumentVersionsAsync(documentId);
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

        // Cấp 1 (Root - Fix cứng): Trạm biến áp & Đường dây
        nodes.Add(new FolderCatalogNodeDto
        {
            Id = "root-tba",
            Name = "Trạm biến áp",
            ParentId = null,
            UnitId = unitId,
            UnitCode = unitCode,
            CreatedDate = DateTime.UtcNow
        });

        nodes.Add(new FolderCatalogNodeDto
        {
            Id = "root-dd",
            Name = "Đường dây",
            ParentId = null,
            UnitId = unitId,
            UnitCode = unitCode,
            CreatedDate = DateTime.UtcNow
        });

        // Cấp 2 under Trạm biến áp: Lưới điện cao áp & Lưới điện trung áp
        nodes.Add(new FolderCatalogNodeDto
        {
            Id = "tba-cao-ap",
            Name = "Lưới điện cao áp",
            ParentId = "root-tba",
            UnitId = unitId,
            UnitCode = unitCode,
            CreatedDate = DateTime.UtcNow
        });

        nodes.Add(new FolderCatalogNodeDto
        {
            Id = "tba-trung-ap",
            Name = "Lưới điện trung áp",
            ParentId = "root-tba",
            UnitId = unitId,
            UnitCode = unitCode,
            CreatedDate = DateTime.UtcNow
        });

        // Cấp 2 under Đường dây: Lưới điện cao áp & Lưới điện trung áp
        nodes.Add(new FolderCatalogNodeDto
        {
            Id = "dd-cao-ap",
            Name = "Lưới điện cao áp",
            ParentId = "root-dd",
            UnitId = unitId,
            UnitCode = unitCode,
            CreatedDate = DateTime.UtcNow
        });

        nodes.Add(new FolderCatalogNodeDto
        {
            Id = "dd-trung-ap",
            Name = "Lưới điện trung áp",
            ParentId = "root-dd",
            UnitId = unitId,
            UnitCode = unitCode,
            CreatedDate = DateTime.UtcNow
        });

        // Tách biệt Substations (type = 1) và Power Lines (type = 2)
        var substations = infrastructures.Where(x => x.InfraTypeId == 1);
        var powerLines = infrastructures.Where(x => x.InfraTypeId == 2);

        // 1. Mapping Trạm biến áp (4 cấp)
        foreach (var sub in substations)
        {
            bool isHighVoltageSub = (sub.Name != null && (sub.Name.Contains("110") || sub.Name.Contains("220") || sub.Name.Contains("500")))
                                    || (sub.Code != null && (sub.Code.Contains("110") || sub.Code.Contains("220") || sub.Code.Contains("500")));

            if (isHighVoltageSub)
            {
                // Cấp 3 
                var subNodeId = $"tba-cao-ap_{sub.Id}";
                nodes.Add(new FolderCatalogNodeDto
                {
                    Id = subNodeId,
                    Name = $"{sub.Name} ({sub.Code})",
                    ParentId = "tba-cao-ap",
                    UnitId = unitId,
                    UnitCode = unitCode,
                    CreatedDate = DateTime.UtcNow
                });

                // Cấp 4 
                var subDossiers = dossiers.Where(d => string.Equals(d.InfrastructureId, sub.Id, StringComparison.OrdinalIgnoreCase) && (d.GridTypeId == 1 || d.GridTypeId == null));
                var dossierGroups = subDossiers
                    .GroupBy(d => new { d.DossierTypeId, d.DossierTypeName })
                    .OrderBy(g => g.Key.DossierTypeName);

                foreach (var g in dossierGroups)
                {
                    if (string.IsNullOrEmpty(g.Key.DossierTypeId)) continue;
                    nodes.Add(new FolderCatalogNodeDto
                    {
                        Id = $"type_{subNodeId}_{g.Key.DossierTypeId}",
                        Name = $"{g.Key.DossierTypeName}",
                        ParentId = subNodeId,
                        UnitId = unitId,
                        UnitCode = unitCode,
                        CreatedDate = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // Cấp 3 
                var subNodeId = $"tba-trung-ap_{sub.Id}";
                nodes.Add(new FolderCatalogNodeDto
                {
                    Id = subNodeId,
                    Name = $"{sub.Name} ({sub.Code})",
                    ParentId = "tba-trung-ap",
                    UnitId = unitId,
                    UnitCode = unitCode,
                    CreatedDate = DateTime.UtcNow
                });

                // Cấp 4 
                var subDossiers = dossiers.Where(d => string.Equals(d.InfrastructureId, sub.Id, StringComparison.OrdinalIgnoreCase) && (d.GridTypeId != 1 || d.GridTypeId == null));
                var dossierGroups = subDossiers
                    .GroupBy(d => new { d.DossierTypeId, d.DossierTypeName })
                    .OrderBy(g => g.Key.DossierTypeName);

                foreach (var g in dossierGroups)
                {
                    if (string.IsNullOrEmpty(g.Key.DossierTypeId)) continue;
                    nodes.Add(new FolderCatalogNodeDto
                    {
                        Id = $"type_{subNodeId}_{g.Key.DossierTypeId}",
                        Name = $"{g.Key.DossierTypeName}",
                        ParentId = subNodeId,
                        UnitId = unitId,
                        UnitCode = unitCode,
                        CreatedDate = DateTime.UtcNow
                    });
                }
            }
        }

        // 2. Mapping Đường dây (4 cấp)
        foreach (var infra in powerLines)
        {
            bool isHighVoltageInfra = (infra.Name != null && (infra.Name.Contains("110") || infra.Name.Contains("220") || infra.Name.Contains("500")))
                                      || (infra.Code != null && (infra.Code.Contains("110") || infra.Code.Contains("220") || infra.Code.Contains("500")));

            if (isHighVoltageInfra)
            {
                // Cấp 3 
                var lineNodeId = $"dd-cao-ap_{infra.Id}";
                nodes.Add(new FolderCatalogNodeDto
                {
                    Id = lineNodeId,
                    Name = $"{infra.Name} ({infra.Code})",
                    ParentId = "dd-cao-ap",
                    UnitId = unitId,
                    UnitCode = unitCode,
                    CreatedDate = DateTime.UtcNow
                });

                // Cấp 4 
                var lineDossiers = dossiers.Where(d => string.Equals(d.InfrastructureId, infra.Id, StringComparison.OrdinalIgnoreCase) && (d.GridTypeId == 1 || d.GridTypeId == null));
                var dossierGroups = lineDossiers
                    .GroupBy(d => new { d.DossierTypeId, d.DossierTypeName })
                    .OrderBy(g => g.Key.DossierTypeName);

                foreach (var g in dossierGroups)
                {
                    if (string.IsNullOrEmpty(g.Key.DossierTypeId)) continue;
                    nodes.Add(new FolderCatalogNodeDto
                    {
                        Id = $"type_{lineNodeId}_{g.Key.DossierTypeId}",
                        Name = $"{g.Key.DossierTypeName}",
                        ParentId = lineNodeId,
                        UnitId = unitId,
                        UnitCode = unitCode,
                        CreatedDate = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // Cấp 3
                var lineNodeId = $"dd-trung-ap_{infra.Id}";
                nodes.Add(new FolderCatalogNodeDto
                {
                    Id = lineNodeId,
                    Name = $"{infra.Name} ({infra.Code})",
                    ParentId = "dd-trung-ap",
                    UnitId = unitId,
                    UnitCode = unitCode,
                    CreatedDate = DateTime.UtcNow
                });

                // Cấp 4 
                var lineDossiers = dossiers.Where(d => string.Equals(d.InfrastructureId, infra.Id, StringComparison.OrdinalIgnoreCase) && (d.GridTypeId != 1 || d.GridTypeId == null));
                var dossierGroups = lineDossiers
                    .GroupBy(d => new { d.DossierTypeId, d.DossierTypeName })
                    .OrderBy(g => g.Key.DossierTypeName);

                foreach (var g in dossierGroups)
                {
                    if (string.IsNullOrEmpty(g.Key.DossierTypeId)) continue;
                    nodes.Add(new FolderCatalogNodeDto
                    {
                        Id = $"type_{lineNodeId}_{g.Key.DossierTypeId}",
                        Name = $"{g.Key.DossierTypeName}",
                        ParentId = lineNodeId,
                        UnitId = unitId,
                        UnitCode = unitCode,
                        CreatedDate = DateTime.UtcNow
                    });
                }
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
