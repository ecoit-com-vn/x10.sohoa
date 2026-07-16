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
    Task<IEnumerable<FolderNodeDto>> GetChildFoldersByParentAsync(Guid parentId);
    Task<IEnumerable<FolderZipDocumentDto>> GetFolderDocumentsForZipAsync(Guid folderId);

    // Document operations
    Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsByFolderAsync(Guid? folderId, DocumentFilterDto filter);
    Task<DocumentListItemDto?> GetDocumentByIdAsync(Guid id);
    Task<Document?> GetDocumentByNameAndFolderAsync(string name, Guid folderId);
    // New: Get document by name within a dossier
    Task<Document?> GetDocumentByNameAndDossierAsync(string name, Guid dossierId);
    // New: Get the highest version number for a document
    Task<int> GetMaxDocumentVersionNumberAsync(Guid documentId);
    Task<EavFormTemplate?> GetEavFormTemplateByDocumentIdAsync(Guid documentId);
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

    // Dossier document operations
    Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsByDossierAsync(Guid dossierId, DossierDocumentFilterDto filter);
    Task<bool> AssignDocumentToDossierAsync(Guid documentId, Guid dossierId, Guid documentTypeId, string modifiedBy);
    Task<bool> UpdateDocumentVersionFilePathAsync(Guid versionId, string filePath, string modifiedBy);
    Task<bool> SoftDeleteDocumentVersionsAsync(Guid documentId, string modifiedBy);
    Task<bool> SoftDeleteDocumentVersionAsync(Guid versionId, string modifiedBy);
    Task<string?> GetOrganizationUnitCodeAsync(long unitId);
    Task<bool> DocumentBelongsToDossierAsync(Guid documentId, Guid dossierId);
    Task<bool> VersionBelongsToDossierAsync(Guid versionId, Guid dossierId);
    Task<bool> IsEquipmentProfileDocumentVersionForEquipmentAsync(Guid equipmentId, Guid versionId);
    Task<Guid?> GetDossierIdByVersionIdAsync(Guid versionId);
    Task<int?> GetDossierPublishStatusIdByVersionIdAsync(Guid versionId);

    // Dossier Catalog tree queries
    Task<UnitQueryDto?> GetUnitInfoAsync(long unitId);
    Task<IEnumerable<DossierTypeQueryDto>> GetActiveDossierTypesWithGridTypeAsync();
    Task<IEnumerable<InfrastructureQueryDto>> GetActiveInfrastructuresByUnitAsync(long unitId);
    Task<IEnumerable<ActiveDossierQueryDto>> GetActiveDossiersByUnitAsync(long unitId);
    Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDossierCatalogDocumentsAsync(
        long unitId, 
        string? infrastructureId, 
        string? dossierTypeId, 
        string? keyword, 
        int page, 
        int pageSize);

    Task<IEnumerable<DocumentOcrIndexHintDto>> GetOcrVersionIndexHintsByDossierIdAsync(Guid dossierId);
    Task<IEnumerable<Guid>> GetActiveVersionIdsByDossierIdAsync(Guid dossierId);
    Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetProfileDocumentsByEquipmentAsync(Guid equipmentId, DossierDocumentFilterDto filter);
}

