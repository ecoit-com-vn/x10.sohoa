using System.Text.Json;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Core.Services;

public interface IDossierDocumentService
{
    Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsAsync(
        Guid dossierId,
        DossierDocumentFilterDto filter);

    Task<DownloadTokenResponse> GetDownloadTokenAsync(
        Guid dossierId,
        Guid versionId,
        CancellationToken cancellationToken = default);

    Task<FileUploadResponse> UploadDirectAsync(
        Guid dossierId,
        Stream fileStream,
        string fileName,
        string mimeType,
        long fileSize,
        Guid documentTypeId,
        int uploadSource,
        string userId,
        long userUnitId,
        string? creatorName,
        CancellationToken cancellationToken);

    Task<InitiateChunkedUploadResponse> InitiateChunkedUploadAsync(
        Guid dossierId,
        string fileName,
        long fileSize,
        string userId,
        CancellationToken cancellationToken);

    Task<string> UploadChunkAsync(
        Guid dossierId,
        string uploadId,
        int chunkNumber,
        Stream chunkStream,
        long chunkSize,
        string userId,
        long userUnitId,
        CancellationToken cancellationToken);

    Task<FileUploadResponse> CompleteChunkedUploadAsync(
        Guid dossierId,
        string uploadId,
        CompleteChunkedUploadRequest request,
        string userId,
        long userUnitId,
        string? creatorName,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MovedDossierDocumentDto>> MoveFromFolderAsync(
        Guid dossierId,
        MoveDocumentsFromFolderRequest request,
        string userId,
        long userUnitId,
        CancellationToken cancellationToken);

    Task<bool> DeleteDocumentAsync(
        Guid dossierId,
        Guid documentId,
        string userId,
        CancellationToken cancellationToken = default);
}

public class DossierDocumentService : IDossierDocumentService
{
    private readonly IDossierService _dossierService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentTypeRepository _documentTypeRepository;
    private readonly IFileUploadService _fileUploadService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFileDownloadTokenService _downloadTokenService;
    private readonly ILogger<DossierDocumentService> _logger;

    public DossierDocumentService(
        IDossierService dossierService,
        IDocumentRepository documentRepository,
        IDocumentTypeRepository documentTypeRepository,
        IFileUploadService fileUploadService,
        IFileStorageService fileStorageService,
        IFileDownloadTokenService downloadTokenService,
        ILogger<DossierDocumentService> logger)
    {
        _dossierService = dossierService ?? throw new ArgumentNullException(nameof(dossierService));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _documentTypeRepository = documentTypeRepository ?? throw new ArgumentNullException(nameof(documentTypeRepository));
        _fileUploadService = fileUploadService ?? throw new ArgumentNullException(nameof(fileUploadService));
        _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
        _downloadTokenService = downloadTokenService ?? throw new ArgumentNullException(nameof(downloadTokenService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsAsync(
        Guid dossierId,
        DossierDocumentFilterDto filter)
    {
        await EnsureDossierExistsAsync(dossierId);
        return await _documentRepository.GetDocumentsByDossierAsync(dossierId, filter);
    }

    public async Task<DownloadTokenResponse> GetDownloadTokenAsync(
        Guid dossierId,
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureDossierExistsAsync(dossierId);

        if (!await _documentRepository.VersionBelongsToDossierAsync(versionId, dossierId))
            throw new KeyNotFoundException("Phiên bản tài liệu không thuộc hồ sơ này");

        var version = await _documentRepository.GetDocumentVersionByIdAsync(versionId);
        if (version == null || string.IsNullOrEmpty(version.FilePath))
            throw new KeyNotFoundException("Phiên bản tài liệu không tồn tại");

        var document = await _documentRepository.GetDocumentByIdAsync(version.DocumentId);
        if (document == null)
            throw new KeyNotFoundException("Tài liệu không tồn tại");

        return await _downloadTokenService.CreateTokenAsync(
            version.FilePath,
            document.Name,
            version.MimeType ?? "application/octet-stream",
            _fileStorageService.DossierBucketName,
            cancellationToken);
    }

    public async Task<FileUploadResponse> UploadDirectAsync(
        Guid dossierId,
        Stream fileStream,
        string fileName,
        string mimeType,
        long fileSize,
        Guid documentTypeId,
        int uploadSource,
        string userId,
        long userUnitId,
        string? creatorName,
        CancellationToken cancellationToken)
    {
        await _dossierService.EnsureCanEditFormDataAsync(dossierId);
        await EnsureActiveDocumentTypeAsync(documentTypeId);

        var result = await _fileUploadService.UploadFileToDossierDirectAsync(
            fileStream, fileName, mimeType, fileSize, dossierId, documentTypeId, uploadSource, userId, userUnitId, creatorName, cancellationToken);

        await _dossierService.RecordDocumentListChangeAsync(
            dossierId, $"Upload trực tiếp: {fileName}", userId);

        return result;
    }

    public async Task<InitiateChunkedUploadResponse> InitiateChunkedUploadAsync(
        Guid dossierId,
        string fileName,
        long fileSize,
        string userId,
        CancellationToken cancellationToken)
    {
        await _dossierService.EnsureCanEditFormDataAsync(dossierId);
        return await _fileUploadService.InitiateDossierChunkedUploadAsync(
            fileName, fileSize, dossierId, userId, cancellationToken);
    }

    public async Task<string> UploadChunkAsync(
        Guid dossierId,
        string uploadId,
        int chunkNumber,
        Stream chunkStream,
        long chunkSize,
        string userId,
        long userUnitId,
        CancellationToken cancellationToken)
    {
        await _dossierService.EnsureCanEditFormDataAsync(dossierId);
        return await _fileUploadService.UploadDossierChunkAsync(
            uploadId, dossierId, chunkNumber, chunkStream, chunkSize, userUnitId, cancellationToken);
    }

    public async Task<FileUploadResponse> CompleteChunkedUploadAsync(
        Guid dossierId,
        string uploadId,
        CompleteChunkedUploadRequest request,
        string userId,
        long userUnitId,
        string? creatorName,
        CancellationToken cancellationToken)
    {
        await _dossierService.EnsureCanEditFormDataAsync(dossierId);

        if (!request.DocumentTypeId.HasValue || request.DocumentTypeId.Value == Guid.Empty)
            throw new ArgumentException("Loại văn bản (DocumentType) là bắt buộc.");

        await EnsureActiveDocumentTypeAsync(request.DocumentTypeId.Value);

        var session = await _documentRepository.GetUploadSessionAsync(uploadId);
        var fileName = session?.FileName ?? "file";

        var result = await _fileUploadService.CompleteDossierChunkedUploadAsync(
            uploadId, dossierId, request, userId, userUnitId, creatorName, cancellationToken);

        await _dossierService.RecordDocumentListChangeAsync(
            dossierId, $"Upload trực tiếp (chunked): {fileName}", userId);

        return result;
    }

    public async Task<IReadOnlyList<MovedDossierDocumentDto>> MoveFromFolderAsync(
        Guid dossierId,
        MoveDocumentsFromFolderRequest request,
        string userId,
        long userUnitId,
        CancellationToken cancellationToken)
    {
        await _dossierService.EnsureCanEditFormDataAsync(dossierId);

        if (request.DocumentIds == null || request.DocumentIds.Count == 0)
            throw new ArgumentException("Danh sách tài liệu không được để trống");

        if (request.DocumentTypeId == Guid.Empty)
            throw new ArgumentException("Loại văn bản (DocumentType) là bắt buộc.");

        await EnsureActiveDocumentTypeAsync(request.DocumentTypeId);

        var unitCode = await ResolveUnitCodeAsync(userUnitId);
        var movedItems = new List<MovedDossierDocumentDto>();

        foreach (var documentId in request.DocumentIds.Distinct())
        {
            var document = await _documentRepository.GetDocumentByIdAsync(documentId);
            if (document == null)
                throw new KeyNotFoundException($"Không tìm thấy tài liệu {documentId}");

            if (!document.FolderId.HasValue || document.DossierId.HasValue)
                throw new InvalidOperationException($"Tài liệu '{document.Name}' không thuộc kho thư mục hoặc đã được chuyển sang hồ sơ");

            if (!document.LatestVersionId.HasValue || string.IsNullOrEmpty(await GetVersionPathAsync(document.LatestVersionId.Value)))
                throw new InvalidOperationException($"Tài liệu '{document.Name}' chưa có file");

            var folder = await _documentRepository.GetFolderByIdAsync(document.FolderId.Value);
            if (folder == null)
                throw new InvalidOperationException($"Thư mục chứa tài liệu '{document.Name}' không tồn tại");

            if (userUnitId == 0 || userUnitId < folder.UnitId)
                throw new UnauthorizedAccessException($"Bạn không có quyền chuyển tài liệu '{document.Name}' từ thư mục này");

            var version = await _documentRepository.GetDocumentVersionByIdAsync(document.LatestVersionId.Value);
            if (version == null || string.IsNullOrEmpty(version.FilePath))
                throw new InvalidOperationException($"Tài liệu '{document.Name}' chưa có file");

            var destPath = _fileStorageService.BuildDossierObjectKey(unitCode, dossierId, document.Name);
            await _fileStorageService.CopyFileAsync(
                version.FilePath,
                destPath,
                destinationBucketName: _fileStorageService.DossierBucketName,
                cancellationToken: cancellationToken);
            await _fileStorageService.DeleteFileAsync(version.FilePath, cancellationToken: cancellationToken);

            var assigned = await _documentRepository.AssignDocumentToDossierAsync(
                documentId, dossierId, request.DocumentTypeId, userId);
            if (!assigned)
                throw new InvalidOperationException($"Không thể chuyển tài liệu '{document.Name}' sang hồ sơ");

            await _documentRepository.UpdateDocumentVersionFilePathAsync(version.Id, destPath, userId);
            movedItems.Add(new MovedDossierDocumentDto
            {
                DocumentId = documentId,
                VersionId = document.LatestVersionId.Value,
                Name = document.Name
            });
        }

        await _dossierService.RecordDocumentListChangeAsync(
            dossierId,
            $"Chuyển tài liệu từ kho: {string.Join(", ", movedItems.Select(m => m.Name))}",
            userId);

        return movedItems;
    }

    public async Task<bool> DeleteDocumentAsync(
        Guid dossierId,
        Guid documentId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await _dossierService.EnsureCanEditFormDataAsync(dossierId);

        if (!await _documentRepository.DocumentBelongsToDossierAsync(documentId, dossierId))
            throw new KeyNotFoundException("Tài liệu không thuộc hồ sơ này");

        var document = await _documentRepository.GetDocumentByIdAsync(documentId);
        if (document == null)
            return false;

        var versions = await _documentRepository.GetDocumentVersionsAsync(documentId);
        foreach (var version in versions)
        {
            if (!string.IsNullOrEmpty(version.FilePath))
                await _fileStorageService.DeleteFileAsync(
                    version.FilePath,
                    _fileStorageService.DossierBucketName,
                    cancellationToken);
        }

        await _documentRepository.SoftDeleteDocumentVersionsAsync(documentId, userId);
        var deleted = await _documentRepository.DeleteDocumentAsync(documentId, userId);

        if (deleted)
        {
            await _dossierService.RecordDocumentListChangeAsync(
                dossierId, $"Xóa tài liệu: {document.Name}", userId);
        }

        return deleted;
    }

    private async Task EnsureActiveDocumentTypeAsync(Guid documentTypeId)
    {
        var docType = await _documentTypeRepository.GetByIdAsync(documentTypeId);
        if (docType == null)
            throw new KeyNotFoundException("Không tìm thấy loại văn bản.");
        if (!docType.IsActive)
            throw new InvalidOperationException("Loại văn bản đang bị khóa.");
    }

    private async Task EnsureDossierExistsAsync(Guid dossierId)
    {
        var detail = await _dossierService.GetDetailByIdAsync(dossierId);
        if (detail == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {dossierId}");
    }

    private async Task<string> ResolveUnitCodeAsync(long userUnitId)
    {
        if (userUnitId == 0)
            throw new UnauthorizedAccessException("Không thể xác định đơn vị của người dùng");

        var unitCode = await _documentRepository.GetOrganizationUnitCodeAsync(userUnitId);
        if (string.IsNullOrWhiteSpace(unitCode))
            throw new InvalidOperationException("Không tìm thấy mã đơn vị (unit_code)");

        return unitCode.Trim();
    }

    private async Task<string?> GetVersionPathAsync(Guid versionId)
    {
        var version = await _documentRepository.GetDocumentVersionByIdAsync(versionId);
        return version?.FilePath;
    }
}
