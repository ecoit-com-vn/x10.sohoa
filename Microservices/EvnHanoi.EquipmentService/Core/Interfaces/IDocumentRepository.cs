using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDocumentRepository
{
    // Folder operations
    Task<IEnumerable<FolderNodeDto>> GetFolderTreeByUnitAsync(long unitId);
    Task<FolderNodeDto?> GetFolderByIdAsync(Guid id);
    Task<Guid> CreateFolderAsync(Folder folder);
    Task<bool> UpdateFolderAsync(Folder folder);
    Task<bool> DeleteFolderAsync(Guid id, string modifiedBy);
    Task<bool> FolderExistsAsync(Guid id);

    // Document operations
    Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsByFolderAsync(Guid? folderId, DocumentFilterDto filter);
    Task<DocumentListItemDto?> GetDocumentByIdAsync(Guid id);
    Task<Guid> CreateDocumentAsync(Document document);
    Task<bool> UpdateDocumentAsync(Document document);
    Task<bool> DeleteDocumentAsync(Guid id, string modifiedBy);

    // Document Version operations
    Task<Guid> CreateDocumentVersionAsync(DocumentVersion version);
    Task<IEnumerable<DocumentVersionDto>> GetDocumentVersionsAsync(Guid documentId);
    Task<DocumentVersionDto?> GetDocumentVersionByIdAsync(Guid versionId);

    // Upload Session operations (new for file upload system)
    Task<Guid> CreateUploadSessionAsync(UploadSession session);
    Task<UploadSession?> GetUploadSessionAsync(string uploadId);
    Task<bool> UpdateUploadSessionAsync(UploadSession session);
    Task<bool> CompleteUploadSessionAsync(string uploadId, string modifiedBy);
    Task<int> DeleteExpiredUploadSessionsAsync();
}
