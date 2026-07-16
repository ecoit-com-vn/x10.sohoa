using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace EvnHanoi.EquipmentService.Core.Services;

/// <summary>
/// Service cho upload file - orchestrate toàn bộ business logic
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Upload file trực tiếp (≤10MB) - orchestrate toàn bộ flow
    /// </summary>
    Task<FileUploadResponse> UploadFileDirectAsync(
        Stream fileStream,
        string fileName,
        string mimeType,
        long fileSize,
        Guid folderId,
        int uploadSource,
        string userId,
        long userUnitId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Khởi tạo chunked upload session
    /// </summary>
    Task<InitiateChunkedUploadResponse> InitiateChunkedUploadAsync(
        string fileName,
        long fileSize,
        Guid folderId,
        string userId,
        long userUnitId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Upload chunk (từ DigitizationService gọi)
    /// </summary>
    Task<string> UploadChunkAsync(
        string uploadId,
        int chunkNumber,
        Stream chunkStream,
        long chunkSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hoàn tất chunked upload - merge + scan + create version
    /// </summary>
    Task<FileUploadResponse> CompleteChunkedUploadAsync(
        string uploadId,
        CompleteChunkedUploadRequest request,
        string userId,
        CancellationToken cancellationToken);

    Task<FileUploadResponse> UploadFileToDossierDirectAsync(
        Stream fileStream,
        string fileName,
        string mimeType,
        long fileSize,
        Guid dossierId,
        Guid documentTypeId,
        int uploadSource,
        string userId,
        long userUnitId,
        string? creatorName,
        CancellationToken cancellationToken);

    Task<InitiateChunkedUploadResponse> InitiateDossierChunkedUploadAsync(
        string fileName,
        long fileSize,
        Guid dossierId,
        string userId,
        CancellationToken cancellationToken);

    Task<string> UploadDossierChunkAsync(
        string uploadId,
        Guid dossierId,
        int chunkNumber,
        Stream chunkStream,
        long chunkSize,
        long userUnitId,
        CancellationToken cancellationToken);

    Task<FileUploadResponse> CompleteDossierChunkedUploadAsync(
        string uploadId,
        Guid dossierId,
        CompleteChunkedUploadRequest request,
        string userId,
        long userUnitId,
        string? creatorName,
        CancellationToken cancellationToken);
}

public class FileUploadService : IFileUploadService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IClamAvService _antivirusService;
    private readonly IMimeTypeValidationService _mimeTypeValidator;
    private readonly IConfiguration _config;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(
        IDocumentRepository documentRepository,
        IFileStorageService fileStorageService,
        IClamAvService antivirusService,
        IMimeTypeValidationService mimeTypeValidator,
        IConfiguration config,
        ILogger<FileUploadService> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
        _antivirusService = antivirusService ?? throw new ArgumentNullException(nameof(antivirusService));
        _mimeTypeValidator = mimeTypeValidator ?? throw new ArgumentNullException(nameof(mimeTypeValidator));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FileUploadResponse> UploadFileDirectAsync(
        Stream fileStream,
        string fileName,
        string mimeType,
        long fileSize,
        Guid folderId,
        int uploadSource,
        string userId,
        long userUnitId,
        CancellationToken cancellationToken)
    {
        try
        {
            // ===== VALIDATION =====
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Tên file không được để trống");

            var folder = await ValidateFolderPermissionAsync(folderId, userUnitId);

            // ===== MIME TYPE & SIGNATURE VALIDATION =====
            await ValidateMimeTypeAsync(mimeType);
            ValidateMagicBytes(fileStream, mimeType);

            // ===== ANTIVIRUS SCAN =====
            _logger.LogInformation("Starting antivirus scan for {FileName}", fileName);
            var scanResult = await _antivirusService.ScanFileAsync(
                fileStream,
                fileName,
                cancellationToken);

            if (!scanResult.IsClean)
            {
                _logger.LogWarning("File infected: {FileName} - Threat: {Threat}", fileName, scanResult.Threat);
                throw new InvalidOperationException($"File bị phát hiện chứa mã độc: {scanResult.Threat}");
            }

            // ===== UPLOAD TO MINIO (theo đơn vị + thư mục) =====
            fileStream.Seek(0, SeekOrigin.Begin);
            var (minioPath, minioVersionId) = await _fileStorageService.UploadFileAsync(
                fileStream,
                fileName,
                mimeType,
                fileSize,
                ResolveUnitCode(folder),
                folderId,
                cancellationToken);

            _logger.LogInformation("File uploaded to MinIO: {MinioPath} (Version: {VersionId})", minioPath, minioVersionId);

            // ===== CREATE OR UPDATE DOCUMENT & VERSION =====
            Guid documentId;
            int versionNumber = 1;

            var existingDoc = await _documentRepository.GetDocumentByNameAndFolderAsync(fileName, folderId);
            if (existingDoc != null)
            {
                documentId = existingDoc.Id;
                var versions = await _documentRepository.GetDocumentVersionsAsync(documentId);
                versionNumber = versions.Any() ? versions.Max(v => v.VersionNumber) + 1 : 1;
                _logger.LogInformation("Found existing document with same name '{FileName}' (DocumentId: {DocumentId}). Incrementing to version {VersionNumber}.", 
                    fileName, documentId, versionNumber);
            }
            else
            {
                var document = new Document
                {
                    Name = fileName,
                    FolderId = folderId,
                    Status = "Active",
                    CreatedBy = userId
                };
                documentId = await _documentRepository.CreateDocumentAsync(document);
            }

            var version = new DocumentVersion
            {
                DocumentId = documentId,
                VersionNumber = versionNumber,
                UploadSource = uploadSource,
                FilePath = minioPath,
                MinioVersionId = minioVersionId,
                FileSize = fileSize,
                MimeType = mimeType,
                ChunksCount = 1,  // Direct upload
                CreatedBy = userId
            };

            var versionId = await _documentRepository.CreateDocumentVersionAsync(version);

            _logger.LogInformation("Successfully uploaded file: {FileName} (DocumentId: {DocumentId}, VersionId: {VersionId}, VersionNumber: {VersionNumber})",
                fileName, documentId, versionId, versionNumber);

            return new FileUploadResponse
            {
                DocumentVersionId = versionId,
                DocumentId = documentId,
                VersionNumber = versionNumber,
                Status = "Active"
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validation failed for upload: {FileName}", fileName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<InitiateChunkedUploadResponse> InitiateChunkedUploadAsync(
        string fileName,
        long fileSize,
        Guid folderId,
        string userId,
        long userUnitId,
        CancellationToken cancellationToken)
    {
        try
        {
            // ===== VALIDATION =====
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Tên file không được để trống");

            await ValidateFolderPermissionAsync(folderId, userUnitId);

            var maxFileSize = _config.GetValue<long>("FileUpload:MaxFileSizeBytes");
            if (fileSize > maxFileSize)
                throw new InvalidOperationException($"File quá lớn. Tối đa {maxFileSize / (1024 * 1024)} MB");

            // ===== CALCULATE CHUNKS =====
            var chunkSize = _config.GetValue<int>("FileUpload:ChunkSizeBytes");
            var totalChunks = (int)Math.Ceiling((double)fileSize / chunkSize);

            // ===== CREATE UPLOAD SESSION =====
            var uploadId = Guid.NewGuid().ToString("N");
            var session = new UploadSession
            {
                UploadId = uploadId,
                FolderId = folderId,
                FileName = fileName,
                TotalChunks = totalChunks,
                Status = "InProgress",
                CreatedDate = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_config.GetValue<int>("FileUpload:UploadSessionExpiryMinutes")),
                CreatedBy = userId
            };

            await _documentRepository.CreateUploadSessionAsync(session);

            _logger.LogInformation("Initiated chunked upload: {UploadId} ({FileName}, {TotalChunks} chunks)",
                uploadId, fileName, totalChunks);

            return new InitiateChunkedUploadResponse
            {
                UploadId = uploadId,
                ChunkSize = chunkSize,
                TotalChunks = totalChunks
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating chunked upload: {FileName}", fileName);
            throw;
        }
    }

    public async Task<string> UploadChunkAsync(
        string uploadId,
        int chunkNumber,
        Stream chunkStream,
        long chunkSize,
        CancellationToken cancellationToken)
    {
        try
        {
            // ===== VALIDATION =====
            var session = await _documentRepository.GetUploadSessionAsync(uploadId);
            if (session == null)
                throw new InvalidOperationException("Upload session không tồn tại");

            if (session.Status != "InProgress")
                throw new InvalidOperationException("Upload session không ở trạng thái InProgress");

            if (chunkNumber < 1 || chunkNumber > session.TotalChunks)
                throw new InvalidOperationException("Chunk number không hợp lệ");

            var folder = await _documentRepository.GetFolderByIdAsync(session.FolderId!.Value);
            if (folder == null)
                throw new InvalidOperationException("Thư mục của upload session không tồn tại");

            // ===== UPLOAD CHUNK (theo đơn vị) =====
            var eTag = await _fileStorageService.UploadChunkAsync(
                uploadId,
                chunkNumber,
                chunkStream,
                chunkSize,
                ResolveUnitCode(folder),
                cancellationToken);

            _logger.LogInformation("Uploaded chunk {ChunkNumber}/{TotalChunks} for session {UploadId}",
                chunkNumber, session.TotalChunks, uploadId);

            return eTag;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading chunk {ChunkNumber} for session {UploadId}", chunkNumber, uploadId);
            throw;
        }
    }

    public async Task<FileUploadResponse> CompleteChunkedUploadAsync(
        string uploadId,
        CompleteChunkedUploadRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            // ===== VALIDATION =====
            var session = await _documentRepository.GetUploadSessionAsync(uploadId);
            if (session == null)
                throw new InvalidOperationException("Upload session không tồn tại");

            if (request.Parts.Count != session.TotalChunks)
                throw new InvalidOperationException($"Thiếu chunks. Cần {session.TotalChunks}, nhận được {request.Parts.Count}");

            var folder = await _documentRepository.GetFolderByIdAsync(session.FolderId!.Value);
            if (folder == null)
                throw new InvalidOperationException("Thư mục của upload session không tồn tại");

            // ===== MERGE CHUNKS (theo đơn vị + thư mục) =====
            var (mergedPath, mergedSize, minioVersionId) = await _fileStorageService.MergeChunksAsync(
                uploadId,
                session.TotalChunks,
                ResolveUnitCode(folder),
                session.FolderId!.Value,
                session.FileName,
                cancellationToken);
            _logger.LogInformation("Merged chunks for session {UploadId}: {MergedPath} (Version: {VersionId})", uploadId, mergedPath, minioVersionId);

            // ===== CREATE OR UPDATE DOCUMENT & VERSION =====
            Guid documentId;
            int versionNumber = 1;

            var existingDoc = await _documentRepository.GetDocumentByNameAndFolderAsync(session.FileName, session.FolderId!.Value);
            if (existingDoc != null)
            {
                documentId = existingDoc.Id;
                var versions = await _documentRepository.GetDocumentVersionsAsync(documentId);
                versionNumber = versions.Any() ? versions.Max(v => v.VersionNumber) + 1 : 1;
                _logger.LogInformation("Found existing document with same name '{FileName}' (DocumentId: {DocumentId}) in chunked upload. Incrementing version to {VersionNumber}.",
                    session.FileName, documentId, versionNumber);
            }
            else
            {
                var document = new Document
                {
                    Name = session.FileName,
                    FolderId = session.FolderId,
                    Status = "Active",
                    CreatedBy = userId
                };
                documentId = await _documentRepository.CreateDocumentAsync(document);
            }

            var version = new DocumentVersion
            {
                DocumentId = documentId,
                VersionNumber = versionNumber,
                UploadSource = 1,  // 1 = Folder upload
                FilePath = mergedPath,
                MinioVersionId = minioVersionId,
                FileSize = mergedSize,
                MimeType = "application/octet-stream",
                UploadSessionId = session.Id,
                ChunksCount = session.TotalChunks,
                CreatedBy = userId
            };

            var versionId = await _documentRepository.CreateDocumentVersionAsync(version);

            // ===== CLEANUP =====
            await _fileStorageService.DeleteUploadSessionAsync(
                uploadId,
                session.TotalChunks,
                ResolveUnitCode(folder),
                cancellationToken);
            await _documentRepository.CompleteUploadSessionAsync(uploadId, userId);

            _logger.LogInformation("Completed chunked upload: {UploadId} ({FileName}), Version: {VersionNumber}", uploadId, session.FileName, versionNumber);

            return new FileUploadResponse
            {
                DocumentVersionId = versionId,
                DocumentId = documentId,
                VersionNumber = versionNumber,
                Status = "Active"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing chunked upload {UploadId}", uploadId);
            throw;
        }
    }

    public async Task<FileUploadResponse> UploadFileToDossierDirectAsync(
        Stream fileStream,
        string fileName,
        string mimeType,
        long fileSize,
        Guid dossierId,
        Guid documentTypeId,
        int uploadSource,
        string userId,
        long userUnitId,
        string? creatorName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Tên file không được để trống");

        var unitCode = await ResolveUnitCodeFromUserAsync(userUnitId);

        await ValidateMimeTypeAsync(mimeType);
        ValidateMagicBytes(fileStream, mimeType);

        var scanResult = await _antivirusService.ScanFileAsync(fileStream, fileName, cancellationToken);
        if (!scanResult.IsClean)
            throw new InvalidOperationException($"File bị phát hiện chứa mã độc: {scanResult.Threat}");

        fileStream.Seek(0, SeekOrigin.Begin);
        var pageCount = DocumentPageCountDetector.Detect(fileStream, fileName, mimeType);

        fileStream.Seek(0, SeekOrigin.Begin);
        var (minioPath, minioVersionId) = await _fileStorageService.UploadFileToDossierAsync(
            fileStream, fileName, mimeType, fileSize, unitCode, dossierId, cancellationToken);

        // Check for existing document in dossier
        var existingDoc = await _documentRepository.GetDocumentByNameAndDossierAsync(fileName, dossierId);
        Guid documentId;
        int versionNumber;
        if (existingDoc != null)
        {
            documentId = existingDoc.Id;
            // Get current max version number and increment
            var maxVersion = await _documentRepository.GetMaxDocumentVersionNumberAsync(documentId);
            versionNumber = maxVersion + 1;
            _logger.LogInformation("Found existing document in dossier '{FileName}' (DocumentId: {DocumentId}). Incrementing to version {VersionNumber}.",
                fileName, documentId, versionNumber);
        }
        else
        {
            var document = new Document
            {
                Name = fileName,
                DossierId = dossierId,
                DocumentTypeId = documentTypeId,
                Status = "Active",
                CreatedBy = userId,
                CreatorName = creatorName
            };
            documentId = await _documentRepository.CreateDocumentAsync(document);
            versionNumber = 1;
        }

        var version = new DocumentVersion
        {
            DocumentId = documentId,
            VersionNumber = versionNumber,
            UploadSource = uploadSource,
            FilePath = minioPath,
            MinioVersionId = minioVersionId,
            FileSize = fileSize,
            MimeType = mimeType,
            PageCount = pageCount,
            ChunksCount = 1,
            CreatedBy = userId
        };
        var versionId = await _documentRepository.CreateDocumentVersionAsync(version);

        return new FileUploadResponse
        {
            DocumentVersionId = versionId,
            DocumentId = documentId,
            VersionNumber = versionNumber,
            Status = "Active"
        };
    }

    public async Task<InitiateChunkedUploadResponse> InitiateDossierChunkedUploadAsync(
        string fileName,
        long fileSize,
        Guid dossierId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Tên file không được để trống");

        var maxFileSize = _config.GetValue<long>("FileUpload:MaxFileSizeBytes");
        if (fileSize > maxFileSize)
            throw new InvalidOperationException($"File quá lớn. Tối đa {maxFileSize / (1024 * 1024)} MB");

        var chunkSize = _config.GetValue<int>("FileUpload:ChunkSizeBytes");
        var totalChunks = (int)Math.Ceiling((double)fileSize / chunkSize);
        var uploadId = Guid.NewGuid().ToString("N");

        var session = new UploadSession
        {
            UploadId = uploadId,
            DossierId = dossierId,
            FileName = fileName,
            TotalChunks = totalChunks,
            Status = "InProgress",
            CreatedDate = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_config.GetValue<int>("FileUpload:UploadSessionExpiryMinutes")),
            CreatedBy = userId
        };
        await _documentRepository.CreateUploadSessionAsync(session);

        return new InitiateChunkedUploadResponse
        {
            UploadId = uploadId,
            ChunkSize = chunkSize,
            TotalChunks = totalChunks
        };
    }

    public async Task<string> UploadDossierChunkAsync(
        string uploadId,
        Guid dossierId,
        int chunkNumber,
        Stream chunkStream,
        long chunkSize,
        long userUnitId,
        CancellationToken cancellationToken)
    {
        var session = await ValidateDossierUploadSessionAsync(uploadId, dossierId);
        if (chunkNumber < 1 || chunkNumber > session.TotalChunks)
            throw new InvalidOperationException("Chunk number không hợp lệ");

        var unitCode = await ResolveUnitCodeFromUserAsync(userUnitId);
        return await _fileStorageService.UploadChunkAsync(
            uploadId, chunkNumber, chunkStream, chunkSize, unitCode, cancellationToken);
    }

    public async Task<FileUploadResponse> CompleteDossierChunkedUploadAsync(
        string uploadId,
        Guid dossierId,
        CompleteChunkedUploadRequest request,
        string userId,
        long userUnitId,
        string? creatorName,
        CancellationToken cancellationToken)
    {
        var session = await ValidateDossierUploadSessionAsync(uploadId, dossierId);
        if (request.Parts.Count != session.TotalChunks)
            throw new InvalidOperationException($"Thiếu chunks. Cần {session.TotalChunks}, nhận được {request.Parts.Count}");

        var unitCode = await ResolveUnitCodeFromUserAsync(userUnitId);
        var (mergedPath, mergedSize, minioVersionId) = await _fileStorageService.MergeChunksToDossierAsync(
            uploadId, session.TotalChunks, unitCode, dossierId, session.FileName, cancellationToken);

        var pageCount = 0;
        try
        {
            await using var mergedFile = await _fileStorageService.DownloadFileAsync(
                mergedPath,
                _fileStorageService.DossierBucketName,
                minioVersionId,
                cancellationToken);
            pageCount = DocumentPageCountDetector.Detect(mergedFile, session.FileName, mimeType: null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Không đọc được số trang sau chunked upload hồ sơ {UploadId} ({FileName})",
                uploadId, session.FileName);
        }

        var document = new Document
        {
            Name = session.FileName,
            DossierId = dossierId,
            DocumentTypeId = request.DocumentTypeId,
            Status = "Active",
            CreatedBy = userId,
            CreatorName = creatorName
        };
        var documentId = await _documentRepository.CreateDocumentAsync(document);

        var version = new DocumentVersion
        {
            DocumentId = documentId,
            VersionNumber = 1,
            UploadSource = 3,
            FilePath = mergedPath,
            MinioVersionId = minioVersionId,
            FileSize = mergedSize,
            MimeType = "application/octet-stream",
            PageCount = pageCount,
            UploadSessionId = session.Id,
            ChunksCount = session.TotalChunks,
            CreatedBy = userId
        };
        var versionId = await _documentRepository.CreateDocumentVersionAsync(version);

        await _fileStorageService.DeleteUploadSessionAsync(
            uploadId, session.TotalChunks, unitCode, cancellationToken);
        await _documentRepository.CompleteUploadSessionAsync(uploadId, userId);

        return new FileUploadResponse
        {
            DocumentVersionId = versionId,
            DocumentId = documentId,
            VersionNumber = 1,
            Status = "Active"
        };
    }

    // ===== PRIVATE HELPERS =====

    private async Task<UploadSession> ValidateDossierUploadSessionAsync(string uploadId, Guid dossierId)
    {
        var session = await _documentRepository.GetUploadSessionAsync(uploadId);
        if (session == null)
            throw new InvalidOperationException("Upload session không tồn tại");
        if (session.Status != "InProgress")
            throw new InvalidOperationException("Upload session không ở trạng thái InProgress");
        if (session.DossierId != dossierId)
            throw new UnauthorizedAccessException("Upload session không thuộc hồ sơ này");
        return session;
    }

    private async Task<string> ResolveUnitCodeFromUserAsync(long userUnitId)
    {
        if (userUnitId == 0)
            throw new UnauthorizedAccessException("Không thể xác định đơn vị của người dùng");

        var unitCode = await _documentRepository.GetOrganizationUnitCodeAsync(userUnitId);
        if (string.IsNullOrWhiteSpace(unitCode))
            throw new InvalidOperationException("Không tìm thấy mã đơn vị (unit_code)");

        return unitCode.Trim();
    }

    private async Task<FolderNodeDto> ValidateFolderPermissionAsync(Guid folderId, long userUnitId)
    {
        var folder = await _documentRepository.GetFolderByIdAsync(folderId);
        if (folder == null)
            throw new InvalidOperationException("Thư mục không tồn tại");

        // Cây thư mục kho chỉ hiển thị folder thuộc đúng unit_id JWT — so khớp tuyệt đối, không so sánh số học ID.
        if (userUnitId == 0 || folder.UnitId != userUnitId)
            throw new UnauthorizedAccessException("Bạn không có quyền upload file vào thư mục này");

        return folder;
    }

    private static string ResolveUnitCode(FolderNodeDto folder)
    {
        if (string.IsNullOrWhiteSpace(folder.UnitCode))
            throw new InvalidOperationException("Không tìm thấy mã đơn vị (unit_code) của thư mục");

        return folder.UnitCode.Trim();
    }

    private async Task ValidateMimeTypeAsync(string mimeType)
    {
        var isMimeTypeAllowed = await _mimeTypeValidator.IsAllowedMimeTypeAsync(mimeType);
        if (!isMimeTypeAllowed)
            throw new ArgumentException($"Loại file không được hỗ trợ: {mimeType}");
    }

    private void ValidateMagicBytes(Stream fileStream, string mimeType)
    {
        using var memStream = new MemoryStream();
        fileStream.CopyTo(memStream);
        memStream.Seek(0, SeekOrigin.Begin);
        
        if (!_mimeTypeValidator.ValidateMagicBytes(memStream, mimeType))
            throw new ArgumentException("File bị nghi ngờ - chữ ký file không khớp với loại file");
        
        fileStream.Seek(0, SeekOrigin.Begin);
    }
}
