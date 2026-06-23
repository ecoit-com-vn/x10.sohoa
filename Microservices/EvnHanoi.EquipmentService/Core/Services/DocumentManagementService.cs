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
}
