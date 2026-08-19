using System.Text.Json;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.DTOs.DigitalSignature;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EvnHanoi.EquipmentService.Core.Services;

public interface IDossierDocumentService
{
    Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsAsync(
        Guid dossierId,
        DossierDocumentFilterDto filter);

    Task<byte[]> ExportDocumentsAsync(Guid dossierId, string? keyword = null);

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

    Task AbortChunkedUploadAsync(
        Guid dossierId,
        string uploadId,
        string userId,
        long userUnitId,
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

    Task<bool> RollbackDocumentVersionAsync(
        Guid dossierId,
        Guid versionId,
        string userId,
        long userUnitId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteDocumentVersionAsync(
        Guid dossierId,
        Guid versionId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ký số một tài liệu trong hồ sơ (tích hợp API ký số ngoài) — tạo phiên bản mới (file đã ký)
    /// khi thành công. Lỗi nghiệp vụ (chưa cấu hình ns_ID, chứng thư hết hạn, ký thất bại, không
    /// gọi được API ngoài...) trả về qua <see cref="SignDocumentResult.Success"/> = false, KHÔNG
    /// ném exception — ngoại trừ lỗi "tài liệu/hồ sơ không tồn tại" (KeyNotFoundException) và lỗi
    /// trạng thái không hợp lệ để chỉnh sửa (InvalidOperationException), theo đúng quy ước các
    /// action khác trong service này.
    /// </summary>
    Task<SignDocumentResult> SignDocumentAsync(
        Guid dossierId,
        Guid documentId,
        string userId,
        string userName,
        long userUnitId,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy biểu mẫu EAV theo loại văn bản gắn với phiên bản tài liệu.</summary>
    Task<EavFormTemplate?> GetFormTemplateForDocumentVersionAsync(
        Guid dossierId,
        Guid versionId);

    /// <summary>
    /// Loại văn bản gắn với loại hồ sơ của dossier (tab Tài liệu).
    /// Không có liên kết cấu hình → trả danh sách rỗng (không fallback tất cả loại văn bản).
    /// </summary>
    Task<IReadOnlyList<DocumentType>> GetDocumentTypesForDossierAsync(Guid dossierId);
}

public class DossierDocumentService : IDossierDocumentService
{
    private readonly IDossierService _dossierService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentTypeRepository _documentTypeRepository;
    private readonly IFileUploadService _fileUploadService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFileDownloadTokenService _downloadTokenService;
    private readonly IFolderAllocationRepository _folderAllocationRepository;
    private readonly IDocumentTextIndexNotifier _documentTextIndexNotifier;
    private readonly IKySoClient _kySoClient;
    private readonly IIdentityServiceClient _identityServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DossierDocumentService> _logger;

    public DossierDocumentService(
        IDossierService dossierService,
        IDocumentRepository documentRepository,
        IDocumentTypeRepository documentTypeRepository,
        IFileUploadService fileUploadService,
        IFileStorageService fileStorageService,
        IFileDownloadTokenService downloadTokenService,
        IFolderAllocationRepository folderAllocationRepository,
        IDocumentTextIndexNotifier documentTextIndexNotifier,
        IKySoClient kySoClient,
        IIdentityServiceClient identityServiceClient,
        IConfiguration configuration,
        ILogger<DossierDocumentService> logger)
    {
        _dossierService = dossierService ?? throw new ArgumentNullException(nameof(dossierService));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _documentTypeRepository = documentTypeRepository ?? throw new ArgumentNullException(nameof(documentTypeRepository));
        _fileUploadService = fileUploadService ?? throw new ArgumentNullException(nameof(fileUploadService));
        _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
        _downloadTokenService = downloadTokenService ?? throw new ArgumentNullException(nameof(downloadTokenService));
        _folderAllocationRepository = folderAllocationRepository ?? throw new ArgumentNullException(nameof(folderAllocationRepository));
        _documentTextIndexNotifier = documentTextIndexNotifier ?? throw new ArgumentNullException(nameof(documentTextIndexNotifier));
        _kySoClient = kySoClient ?? throw new ArgumentNullException(nameof(kySoClient));
        _identityServiceClient = identityServiceClient ?? throw new ArgumentNullException(nameof(identityServiceClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(IEnumerable<DocumentListItemDto> Items, int TotalCount)> GetDocumentsAsync(
        Guid dossierId,
        DossierDocumentFilterDto filter)
    {
        await EnsureDossierExistsAsync(dossierId);
        return await _documentRepository.GetDocumentsByDossierAsync(dossierId, filter);
    }

    public async Task<byte[]> ExportDocumentsAsync(Guid dossierId, string? keyword = null)
    {
        var filter = new DossierDocumentFilterDto
        {
            Keyword = keyword,
            Page = 1,
            PageSize = int.MaxValue
        };
        var (items, _) = await GetDocumentsAsync(dossierId, filter);

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Danh sach tai lieu");
        string[] headers =
        {
            "STT",
            "Ten tai lieu",
            "Loai van ban",
            "Dinh dang",
            "Nguoi tao",
            "Ngay tao",
            "Trang thai OCR",
            "Trang thai boc tach"
        };

        for (var column = 0; column < headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = headers[column];

        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

        var rowIndex = 2;
        var sequence = 0;
        foreach (var item in items)
        {
            worksheet.Cell(rowIndex, 1).Value = ++sequence;
            worksheet.Cell(rowIndex, 2).Value = item.Name;
            worksheet.Cell(rowIndex, 3).Value = item.DocumentTypeName ?? string.Empty;
            worksheet.Cell(rowIndex, 4).Value = item.MimeType ?? string.Empty;
            worksheet.Cell(rowIndex, 5).Value = item.CreatedByName ?? item.CreatedBy ?? string.Empty;
            worksheet.Cell(rowIndex, 6).Value = item.CreatedDate;
            worksheet.Cell(rowIndex, 6).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            worksheet.Cell(rowIndex, 7).Value = item.OcrProgress?.Status ?? string.Empty;
            worksheet.Cell(rowIndex, 8).Value = item.ExtractionResult?.Status ?? string.Empty;
            rowIndex++;
        }

        if (rowIndex > 2)
        {
            var dataRange = worksheet.Range(1, 1, rowIndex - 1, headers.Length);
            dataRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
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

    public async Task AbortChunkedUploadAsync(
        Guid dossierId,
        string uploadId,
        string userId,
        long userUnitId,
        CancellationToken cancellationToken)
    {
        await _fileUploadService.AbortDossierChunkedUploadAsync(uploadId, dossierId, userId, userUnitId, cancellationToken);
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

            // Check folder allocation (ADMIN bypasses, subfolder inheritance allowed)
            var isAdmin = await _folderAllocationRepository.IsUserAdminAsync(userId);
            if (!isAdmin)
            {
                var activeAllocations = await _folderAllocationRepository.GetActiveAllocationsByUserAsync(userId);
                var allocatedFolderIds = activeAllocations.Select(a => a.FolderId).ToHashSet();

                bool hasAccess = false;
                Guid? currentFolderId = folder.Id;
                
                while (currentFolderId.HasValue)
                {
                    if (allocatedFolderIds.Contains(currentFolderId.Value))
                    {
                        hasAccess = true;
                        break;
                    }
                    
                    var parentFolder = await _documentRepository.GetFolderByIdAsync(currentFolderId.Value);
                    currentFolderId = parentFolder?.ParentId;
                }

                if (!hasAccess)
                {
                    throw new UnauthorizedAccessException($"Bạn không có quyền chuyển tài liệu '{document.Name}' từ thư mục '{folder.Name}' do chưa được phân bổ nhập liệu.");
                }
            }

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

        var versions = (await _documentRepository.GetDocumentVersionsAsync(documentId)).ToList();
        foreach (var version in versions)
        {
            if (!string.IsNullOrEmpty(version.FilePath))
                await _fileStorageService.DeleteFileAsync(
                    version.FilePath,
                    _fileStorageService.DossierBucketName,
                    version.MinioVersionId,
                    cancellationToken);
        }

        await _documentRepository.SoftDeleteDocumentVersionsAsync(documentId, userId);
        var deleted = await _documentRepository.DeleteDocumentAsync(documentId, userId);

        if (deleted)
        {
            foreach (var version in versions)
            {
                try
                {
                    await _documentTextIndexNotifier.PublishDeleteAsync(version.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Không publish xóa index tài liệu version {VersionId}.",
                        version.Id);
                }
            }

            await _dossierService.RecordDocumentListChangeAsync(
                dossierId, $"Xóa tài liệu: {document.Name}", userId);
        }

        return deleted;
    }

    public async Task<EavFormTemplate?> GetFormTemplateForDocumentVersionAsync(Guid dossierId, Guid versionId)
    {
        await EnsureDossierExistsAsync(dossierId);

        if (!await _documentRepository.VersionBelongsToDossierAsync(versionId, dossierId))
            throw new KeyNotFoundException("Phiên bản tài liệu không thuộc hồ sơ này.");

        var version = await _documentRepository.GetDocumentVersionByIdAsync(versionId)
            ?? throw new KeyNotFoundException("Phiên bản tài liệu không tồn tại.");

        var template = await _documentRepository.GetEavFormTemplateByDocumentIdAsync(version.DocumentId);
        if (template != null)
            return template;

        var document = await _documentRepository.GetDocumentByIdAsync(version.DocumentId);
        if (document?.DocumentTypeId is Guid docTypeId && docTypeId != Guid.Empty)
        {
            var docType = await _documentTypeRepository.GetByIdAsync(docTypeId);
            if (docType == null)
                throw new KeyNotFoundException("Không tìm thấy loại văn bản của tài liệu.");

            if (!docType.FormId.HasValue || docType.FormId.Value == Guid.Empty)
                throw new InvalidOperationException("Loại văn bản chưa gắn form EAV.");

            return await _dossierService.GetFormTemplateForDossierAsync(dossierId, docType.FormId)
                ?? throw new InvalidOperationException(
                    "Biểu mẫu EAV gắn với loại văn bản không tồn tại hoặc đã bị xóa.");
        }

        return await _dossierService.GetFormTemplateForDossierAsync(dossierId, null);
    }

    public async Task<bool> RollbackDocumentVersionAsync(
        Guid dossierId,
        Guid versionId,
        string userId,
        long userUnitId,
        CancellationToken cancellationToken = default)
    {
        await _dossierService.EnsureCanEditFormDataAsync(dossierId);

        if (!await _documentRepository.VersionBelongsToDossierAsync(versionId, dossierId))
            throw new KeyNotFoundException("Phiên bản tài liệu không thuộc hồ sơ này");

        var targetVersion = await _documentRepository.GetDocumentVersionByIdAsync(versionId);
        if (targetVersion == null || string.IsNullOrEmpty(targetVersion.FilePath))
            throw new KeyNotFoundException("Phiên bản tài liệu không tồn tại hoặc không có file");

        var document = await _documentRepository.GetDocumentByIdAsync(targetVersion.DocumentId);
        if (document == null)
            throw new KeyNotFoundException("Tài liệu không tồn tại");

        var unitCode = await ResolveUnitCodeAsync(userUnitId);

        // Download the target file version
        using var stream = await _fileStorageService.DownloadFileAsync(
            targetVersion.FilePath,
            _fileStorageService.DossierBucketName,
            targetVersion.MinioVersionId,
            cancellationToken);

        // Upload it back to MinIO as a new version
        var (newPath, newMinioVersionId) = await _fileStorageService.UploadFileToDossierAsync(
            stream,
            document.Name ?? "rollback_file",
            targetVersion.MimeType ?? "application/octet-stream",
            targetVersion.FileSize,
            unitCode,
            dossierId,
            cancellationToken);

        // Determine new version number
        var versions = await _documentRepository.GetDocumentVersionsAsync(document.Id);
        var maxVersion = versions.Any() ? versions.Max(v => v.VersionNumber) : 0;

        var newVersion = new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = maxVersion + 1,
            UploadSource = 3, // Rollback/Web
            FilePath = newPath,
            MinioVersionId = newMinioVersionId,
            FileSize = targetVersion.FileSize,
            MimeType = targetVersion.MimeType,
            CreatedBy = userId
        };

        var newVersionId = await _documentRepository.CreateDocumentVersionAsync(newVersion);
        _logger.LogInformation("Successfully rolled back dossier document {DocumentId} to version {VersionNumber} (New VersionId: {NewVersionId})",
            document.Id, targetVersion.VersionNumber, newVersionId);

        return true;
    }

    public async Task<bool> DeleteDocumentVersionAsync(
        Guid dossierId,
        Guid versionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await _dossierService.EnsureCanEditFormDataAsync(dossierId);

        if (!await _documentRepository.VersionBelongsToDossierAsync(versionId, dossierId))
            throw new KeyNotFoundException("Phiên bản tài liệu không thuộc hồ sơ này");

        var version = await _documentRepository.GetDocumentVersionByIdAsync(versionId);
        if (version == null)
            return false;

        if (!string.IsNullOrEmpty(version.FilePath))
        {
            await _fileStorageService.DeleteFileAsync(
                version.FilePath,
                _fileStorageService.DossierBucketName,
                version.MinioVersionId,
                cancellationToken);
        }

        return await _documentRepository.SoftDeleteDocumentVersionAsync(versionId, userId);
    }

    public async Task<SignDocumentResult> SignDocumentAsync(
        Guid dossierId,
        Guid documentId,
        string userId,
        string userName,
        long userUnitId,
        CancellationToken cancellationToken = default)
    {
        await _dossierService.EnsureCanEditFormDataAsync(dossierId);

        if (!await _documentRepository.DocumentBelongsToDossierAsync(documentId, dossierId))
            throw new KeyNotFoundException("Tài liệu không thuộc hồ sơ này");

        var document = await _documentRepository.GetDocumentByIdAsync(documentId);
        if (document == null)
            throw new KeyNotFoundException("Tài liệu không tồn tại");

        if (!document.LatestVersionId.HasValue)
            throw new InvalidOperationException("Tài liệu chưa có phiên bản để ký số.");

        var latestVersion = await _documentRepository.GetDocumentVersionByIdAsync(document.LatestVersionId.Value);
        if (latestVersion == null || string.IsNullOrEmpty(latestVersion.FilePath))
            throw new InvalidOperationException("Tài liệu chưa có file để ký số.");

        // Resolve ns_ID người dùng hiện tại (EVN HRMS) — không có thì báo lỗi nghiệp vụ, không throw.
        var nsIdRaw = await _identityServiceClient.GetCurrentUserSsoNsIdAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(nsIdRaw) || !long.TryParse(nsIdRaw, out var nsId))
        {
            return SignDocumentResult.Fail(
                "Người dùng chưa được cấu hình ns_ID để ký số, vui lòng liên hệ quản trị hệ thống.");
        }

        try
        {
            var serialInfo = await _kySoClient.GetSerialNumberAsync(nsId, cancellationToken);
            if (serialInfo == null || string.IsNullOrWhiteSpace(serialInfo.Serial))
            {
                return SignDocumentResult.Fail("Không lấy được thông tin chứng thư số (serial number) từ hệ thống ký số.");
            }

            if (serialInfo.ValidTo.HasValue && serialInfo.ValidTo.Value < DateTime.Now)
            {
                return SignDocumentResult.Fail("Chứng thư số đã hết hạn.");
            }

            var signatureImageBase64 = await _kySoClient.GetSignatureImageAsync(nsId, cancellationToken);
            if (string.IsNullOrWhiteSpace(signatureImageBase64))
            {
                return SignDocumentResult.Fail("Không lấy được ảnh chữ ký từ hệ thống ký số.");
            }

            byte[] sourceBytes;
            using (var sourceStream = await _fileStorageService.DownloadFileAsync(
                latestVersion.FilePath!,
                _fileStorageService.DossierBucketName,
                latestVersion.MinioVersionId,
                cancellationToken))
            using (var memoryStream = new MemoryStream())
            {
                await sourceStream.CopyToAsync(memoryStream, cancellationToken);
                sourceBytes = memoryStream.ToArray();
            }

            var signRequest = new KySoSignPdfRequest
            {
                AppCode = _configuration["DigitalSignature:AppCode"] ?? "app4",
                // TODO: mật khẩu chứng thư số KHÔNG được commit dạng plaintext thật — cấu hình hiện
                // tại chỉ là placeholder giả, phải chuyển sang secret store an toàn trước production.
                Password = _configuration["DigitalSignature:Password"] ?? string.Empty,
                SerialNumber = serialInfo.Serial!,
                CaType = _configuration.GetValue<int?>("DigitalSignature:CaType") ?? 1,
                Alias = serialInfo.Alias ?? string.Empty,
                DigestAlgorithm = _configuration["DigitalSignature:DigestAlgorithm"] ?? "SHA-1",
                TimestampConfig = new KySoTimestampConfig { UseTimestamp = false },
                DisplayImageConfigBO = BuildDisplayImageConfig(),
                FileImageBase64 = signatureImageBase64,
                FileBase64 = Convert.ToBase64String(sourceBytes)
            };

            var signResult = await _kySoClient.SignPdfAsync(signRequest, cancellationToken);

            if (!signResult.Status || string.IsNullOrEmpty(signResult.SignedFileBase64))
            {
                var errorMessage = signResult.ObjectError?.ToString();
                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = "Ký số thất bại không rõ nguyên nhân.";

                await _documentRepository.CreateDocumentSignHistoryAsync(new DocumentSignHistory
                {
                    DocumentId = document.Id,
                    DocumentVersionId = null,
                    SignerUserId = userId,
                    SignerName = userName,
                    SerialNumber = serialInfo.Serial,
                    SignedAt = null,
                    Status = "Failed",
                    ErrorMessage = Truncate(errorMessage, 2000),
                    CreatedBy = userId
                });

                _logger.LogWarning(
                    "Ký số thất bại tài liệu {DocumentId} — signStatus=false", document.Id);

                return SignDocumentResult.Fail($"Ký số thất bại: {errorMessage}");
            }

            var signedBytes = Convert.FromBase64String(signResult.SignedFileBase64);
            var unitCode = await ResolveUnitCodeAsync(userUnitId);

            (string NewPath, string NewMinioVersionId) uploadResult;
            using (var signedStream = new MemoryStream(signedBytes))
            {
                uploadResult = await _fileStorageService.UploadFileToDossierAsync(
                    signedStream,
                    document.Name ?? "signed_file.pdf",
                    latestVersion.MimeType ?? "application/pdf",
                    signedBytes.LongLength,
                    unitCode,
                    dossierId,
                    cancellationToken);
            }

            var versions = await _documentRepository.GetDocumentVersionsAsync(document.Id);
            var maxVersion = versions.Any() ? versions.Max(v => v.VersionNumber) : 0;

            var newVersion = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = maxVersion + 1,
                UploadSource = 5, // Ký số
                FilePath = uploadResult.NewPath,
                MinioVersionId = uploadResult.NewMinioVersionId,
                FileSize = signedBytes.LongLength,
                MimeType = latestVersion.MimeType ?? "application/pdf",
                CreatedBy = userId
            };

            var newVersionId = await _documentRepository.CreateDocumentVersionAsync(newVersion);
            var signedAt = DateTime.UtcNow;

            await _documentRepository.CreateDocumentSignHistoryAsync(new DocumentSignHistory
            {
                DocumentId = document.Id,
                DocumentVersionId = newVersionId,
                SignerUserId = userId,
                SignerName = userName,
                SerialNumber = serialInfo.Serial,
                SignedAt = signedAt,
                Status = "Success",
                ErrorMessage = null,
                CreatedBy = userId
            });

            await _dossierService.RecordDocumentListChangeAsync(
                dossierId, $"Ký số tài liệu: {document.Name}", userId);

            _logger.LogInformation(
                "Ký số thành công tài liệu {DocumentId} — phiên bản mới {NewVersionId} (v{VersionNumber})",
                document.Id, newVersionId, newVersion.VersionNumber);

            return SignDocumentResult.Ok(newVersionId, newVersion.VersionNumber, signedAt);
        }
        catch (Exception ex) when (
            ex is not KeyNotFoundException &&
            ex is not InvalidOperationException &&
            ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Lỗi khi gọi hệ thống ký số ngoài cho tài liệu {DocumentId}", documentId);
            return SignDocumentResult.Fail("Không thể kết nối tới hệ thống ký số, vui lòng thử lại sau.");
        }
    }

    private KySoDisplayImageConfig BuildDisplayImageConfig()
    {
        var section = _configuration.GetSection("DigitalSignature:DisplayImageConfig");
        var config = new KySoDisplayImageConfig();

        if (!section.Exists())
            return config;

        config.LocateSign = section.GetValue<int?>("LocateSign") ?? config.LocateSign;
        config.NumberPageSign = section.GetValue<int?>("NumberPageSign") ?? config.NumberPageSign;
        config.WidthRectangle = section.GetValue<int?>("WidthRectangle") ?? config.WidthRectangle;
        config.HeightRectangle = section.GetValue<int?>("HeightRectangle") ?? config.HeightRectangle;
        config.MarginLeftOfRectangle = section.GetValue<int?>("MarginLeftOfRectangle") ?? config.MarginLeftOfRectangle;
        config.MarginRightOfRectangle = section.GetValue<int?>("MarginRightOfRectangle") ?? config.MarginRightOfRectangle;
        config.MarginTopOfRectangle = section.GetValue<int?>("MarginTopOfRectangle") ?? config.MarginTopOfRectangle;
        config.MarginBottomOfRectangle = section.GetValue<int?>("MarginBottomOfRectangle") ?? config.MarginBottomOfRectangle;
        config.Contact = section["Contact"] ?? config.Contact;
        config.Reason = section["Reason"] ?? config.Reason;
        config.Location = section["Location"] ?? config.Location;

        return config;
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    public async Task<IReadOnlyList<DocumentType>> GetDocumentTypesForDossierAsync(Guid dossierId)
    {
        var detail = await _dossierService.GetDetailByIdAsync(dossierId)
            ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {dossierId}");

        if (detail.DossierTypeId == Guid.Empty)
            return Array.Empty<DocumentType>();

        return await _documentTypeRepository.GetActiveByDossierTypeIdAsync(detail.DossierTypeId);
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
