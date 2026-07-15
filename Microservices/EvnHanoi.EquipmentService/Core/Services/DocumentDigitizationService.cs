using System.IO;
using System.Text;
using System.Text.Json;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Core.Services;

public class DocumentDigitizationService : IDocumentDigitizationService
{
    private const string DigitizationExchange = "digitization.topic";
    private const string OcrTaskRoutingKey = "ocr.process.task";
    private const string ExtractionTaskRoutingKey = "extraction.process.task";

    private readonly IDocumentDigitizationRepository _repository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentTypeRepository _documentTypeRepository;
    private readonly IDossierService _dossierService;
    private readonly IEavFormTemplateRepository _formTemplateRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IMessageProducer _messageProducer;
    private readonly IDocumentTextIndexNotifier _documentTextIndexNotifier;
    private readonly IDigitizationProgressNotifier _progressNotifier;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentDigitizationService> _logger;

    public DocumentDigitizationService(
        IDocumentDigitizationRepository repository,
        IDocumentRepository documentRepository,
        IDocumentTypeRepository documentTypeRepository,
        IDossierService dossierService,
        IEavFormTemplateRepository formTemplateRepository,
        IFileStorageService fileStorageService,
        IMessageProducer messageProducer,
        IDocumentTextIndexNotifier documentTextIndexNotifier,
        IDigitizationProgressNotifier progressNotifier,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentDigitizationService> logger)
    {
        _repository = repository;
        _documentRepository = documentRepository;
        _documentTypeRepository = documentTypeRepository;
        _dossierService = dossierService;
        _formTemplateRepository = formTemplateRepository;
        _fileStorageService = fileStorageService;
        _messageProducer = messageProducer;
        _documentTextIndexNotifier = documentTextIndexNotifier;
        _progressNotifier = progressNotifier;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<DocumentOcrProgressDto> SubmitForDossierDocumentAsync(
        Guid dossierId,
        Guid documentVersionId,
        SubmitDossierDocumentDigitizationRequest request,
        string userId)
    {
        var dossier = await _dossierService.GetDetailByIdAsync(dossierId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ.");

        if (!await _documentRepository.VersionBelongsToDossierAsync(documentVersionId, dossierId))
            throw new KeyNotFoundException("Phiên bản tài liệu không thuộc hồ sơ này.");

        var version = await _documentRepository.GetDocumentVersionByIdAsync(documentVersionId)
            ?? throw new KeyNotFoundException("Phiên bản tài liệu không tồn tại.");

        if (string.IsNullOrEmpty(version.FilePath))
            throw new InvalidOperationException("Tài liệu chưa có file để xử lý OCR.");

        var formContext = await ResolveFormContextAsync(
            dossier,
            version.DocumentId,
            documentVersionId,
            request.FormSchemaJson?.Trim(),
            request.ExtractPrompt?.Trim(),
            forceReloadFromTemplate: false);

        return await SubmitOcrJobAsync(new SubmitDocumentDigitizationRequest
        {
            DocumentId = version.DocumentId,
            DocumentVersionId = documentVersionId,
            FilePath = version.FilePath,
            BucketName = _fileStorageService.DossierBucketName,
            ProcessOption = request.ProcessOption,
            ExtractPrompt = formContext.ExtractPrompt,
            FormId = formContext.FormId,
            FormName = formContext.FormName,
            FormSchemaJson = formContext.FormSchemaJson
        }, userId, dossierId);
    }

    public async Task<DocumentOcrProgressDto> ReExtractForDossierDocumentAsync(
        Guid dossierId,
        Guid documentVersionId,
        string userId,
        string? formSchemaJsonOverride = null)
    {
        var dossier = await _dossierService.GetDetailByIdAsync(dossierId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ.");

        if (!await _documentRepository.VersionBelongsToDossierAsync(documentVersionId, dossierId))
            throw new KeyNotFoundException("Phiên bản tài liệu không thuộc hồ sơ này.");

        var version = await _documentRepository.GetDocumentVersionByIdAsync(documentVersionId)
            ?? throw new KeyNotFoundException("Phiên bản tài liệu không tồn tại.");

        if (string.IsNullOrEmpty(version.FilePath))
            throw new InvalidOperationException("Tài liệu chưa có file để bóc tách.");

        var progress = await _repository.GetProgressByVersionIdAsync(documentVersionId)
            ?? throw new InvalidOperationException("Tài liệu chưa qua OCR — không thể bóc tách lại.");

        if (progress.Status is "Running" or "Extracting" or "Pending")
            throw new InvalidOperationException("Tài liệu đang được xử lý — vui lòng đợi hoàn tất.");

        if (!IsOcrPhaseComplete(progress))
            throw new InvalidOperationException("OCR chưa hoàn thành — không thể bóc tách lại.");

        var formContext = await ResolveFormContextAsync(
            dossier,
            version.DocumentId,
            documentVersionId,
            formSchemaJsonOverride: formSchemaJsonOverride?.Trim(),
            extractPromptOverride: null,
            forceReloadFromTemplate: string.IsNullOrWhiteSpace(formSchemaJsonOverride));

        var form = BuildExtractionForm(formContext.FormId, formContext.FormName, formContext.FormSchemaJson);

        progress.Phase = "extraction";
        progress.Action = ExtractionTaskRoutingKey;
        progress.Status = "Extracting";
        progress.Progress = 0;
        progress.CurrentPage = 0;
        progress.TotalPages = 0;
        progress.ProcessOption = "ExtractOnly";
        progress.FormJson = formContext.FormSchemaJson;
        progress.ModifiedBy = userId;
        progress.ModifiedDate = DateTime.UtcNow;
        await _repository.UpdateProgressAsync(progress);

        var extractionResult = await _repository.GetExtractionResultByVersionIdAsync(documentVersionId);
        if (extractionResult == null)
        {
            extractionResult = new DocumentExtractionResult
            {
                DocumentId = version.DocumentId,
                DocumentVersionId = documentVersionId,
                OcrProgressId = progress.Id,
                Status = "Pending",
                FormJson = formContext.FormSchemaJson,
                BucketName = _fileStorageService.DossierBucketName,
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow
            };
            await _repository.CreateExtractionResultAsync(extractionResult);
        }
        else
        {
            extractionResult.Status = "Pending";
            extractionResult.ResultJson = null;
            extractionResult.ResultFilePath = null;
            extractionResult.MergedDataJson = null;
            extractionResult.ErrorMessage = null;
            extractionResult.FormJson = formContext.FormSchemaJson;
            extractionResult.ModifiedBy = userId;
            extractionResult.ModifiedDate = DateTime.UtcNow;
            await _repository.UpdateExtractionResultAsync(extractionResult);
        }

        var message = new ExtractionTaskPublishMessage
        {
            FileId = documentVersionId,
            FilePath = version.FilePath,
            BucketName = _fileStorageService.DossierBucketName,
            ExtractPrompt = formContext.ExtractPrompt,
            Form = form,
            FormSchemaJson = formContext.FormSchemaJson
        };

        await _messageProducer.PublishToExchangeAsync(message, DigitizationExchange, ExtractionTaskRoutingKey);
        NotifyProgressInBackground(dossierId, progress);

        await TryPublishDocumentTextIndexAsync(
            documentVersionId,
            _fileStorageService.DossierBucketName,
            version.FilePath,
            progress.TotalPages,
            "re-extract-md-ready");

        _logger.LogInformation(
            "Đã gửi bóc tách lại version {VersionId}, document {DocumentId}, form {FormId}",
            documentVersionId, version.DocumentId, formContext.FormId);

        return MapProgress(progress);
    }

    private sealed record DigitizationFormContext(
        string FormId,
        string FormName,
        string FormSchemaJson,
        string? ExtractPrompt);

    private async Task<DigitizationFormContext> ResolveFormContextAsync(
        DossierDetailDto dossier,
        Guid documentId,
        Guid? documentVersionId,
        string? formSchemaJsonOverride,
        string? extractPromptOverride,
        bool forceReloadFromTemplate)
    {
        var formSchemaJson = forceReloadFromTemplate ? null : formSchemaJsonOverride?.Trim();
        var extractPrompt = extractPromptOverride?.Trim();

        EavFormTemplate? template = await _documentRepository.GetEavFormTemplateByDocumentIdAsync(documentId);
        string formId = template?.Id.ToString() ?? dossier.FormId?.ToString() ?? string.Empty;
        string formName = template?.Name ?? dossier.DossierTypeName ?? "Hồ sơ";

        if (template == null)
        {
            var documentMeta = await _documentRepository.GetDocumentByIdAsync(documentId);
            if (documentMeta?.DocumentTypeId is Guid docTypeId && docTypeId != Guid.Empty)
            {
                var docType = await _documentTypeRepository.GetByIdAsync(docTypeId);
                if (docType == null)
                    throw new InvalidOperationException("Không tìm thấy loại văn bản của tài liệu.");

                if (!docType.FormId.HasValue || docType.FormId.Value == Guid.Empty)
                    throw new InvalidOperationException("Loại văn bản chưa gắn form EAV — không thể bóc tách.");

                template = await _formTemplateRepository.GetByIdAsync(docType.FormId.Value);
                if (template == null)
                {
                    throw new InvalidOperationException(
                        "Biểu mẫu EAV gắn với loại văn bản không tồn tại hoặc đã bị xóa — không thể bóc tách.");
                }

                formId = template.Id.ToString();
                formName = docType.FormName ?? template.Name;
            }
            else if (dossier.FormId is Guid dossierFormId && dossierFormId != Guid.Empty)
            {
                template = await _formTemplateRepository.GetByIdAsync(dossierFormId);
                if (template != null)
                {
                    formId = template.Id.ToString();
                    formName = template.Name;
                }
            }
        }

        if (forceReloadFromTemplate || string.IsNullOrWhiteSpace(formSchemaJson))
        {
            if (template == null)
            {
                if (forceReloadFromTemplate && documentVersionId.HasValue)
                {
                    formSchemaJson = await GetCachedFormSchemaJsonAsync(documentVersionId.Value);
                    if (!string.IsNullOrWhiteSpace(formSchemaJson) && string.IsNullOrEmpty(formId))
                    {
                        formId = dossier.FormId?.ToString() ?? "cached";
                    }
                }

                if (string.IsNullOrWhiteSpace(formSchemaJson))
                    throw new InvalidOperationException("Loại văn bản chưa gắn form EAV — không thể bóc tách.");
            }
            else
            {
                formSchemaJson = template.FormSchema;
                formId = template.Id.ToString();
                formName = template.Name;
            }
        }

        if ((forceReloadFromTemplate || string.IsNullOrWhiteSpace(extractPrompt))
            && !string.IsNullOrWhiteSpace(template?.ExtractionProcess))
        {
            extractPrompt = template.ExtractionProcess.Trim();
        }

        return new DigitizationFormContext(formId, formName, formSchemaJson!, extractPrompt);
    }

    private async Task<string?> GetCachedFormSchemaJsonAsync(Guid documentVersionId)
    {
        var progress = await _repository.GetProgressByVersionIdAsync(documentVersionId);
        if (!string.IsNullOrWhiteSpace(progress?.FormJson))
            return progress.FormJson.Trim();

        var extraction = await _repository.GetExtractionResultByVersionIdAsync(documentVersionId);
        return string.IsNullOrWhiteSpace(extraction?.FormJson) ? null : extraction.FormJson.Trim();
    }

    private static bool IsOcrPhaseComplete(DocumentOcrProgress progress) =>
        progress.Status is "OcrCompleted" or "Completed" or "Extracting"
        || (progress.Status == "Failed" && progress.Phase == "extraction");

    public async Task<DocumentOcrProgressDto> SubmitOcrJobAsync(
        SubmitDocumentDigitizationRequest request,
        string userId,
        Guid? dossierId = null)
    {
        if (string.IsNullOrWhiteSpace(request.FormSchemaJson))
            throw new ArgumentException("FormSchemaJson không được để trống — cần schema EAV để bóc tách.");

        var existing = await _repository.GetProgressByVersionIdAsync(request.DocumentVersionId);
        if (existing != null && existing.Status is "Running" or "Extracting" or "Pending")
            throw new InvalidOperationException("Tài liệu đang được xử lý OCR/bóc tách.");

        var form = BuildExtractionForm(request.FormId, request.FormName, request.FormSchemaJson);

        var progress = new DocumentOcrProgress
        {
            DocumentId = request.DocumentId,
            DocumentVersionId = request.DocumentVersionId,
            Phase = "ocr",
            Action = "ocr.process.task",
            Status = "Pending",
            ProcessOption = request.ProcessOption,
            BucketName = request.BucketName,
            FilePath = request.FilePath,
            FormJson = request.FormSchemaJson,
            Progress = 0,
            CreatedBy = userId,
            CreatedDate = DateTime.UtcNow
        };

        await _repository.CreateProgressAsync(progress);

        var extractionResult = new DocumentExtractionResult
        {
            DocumentId = request.DocumentId,
            DocumentVersionId = request.DocumentVersionId,
            OcrProgressId = progress.Id,
            Status = "Pending",
            FormJson = request.FormSchemaJson,
            BucketName = request.BucketName,
            CreatedBy = userId,
            CreatedDate = DateTime.UtcNow
        };
        await _repository.CreateExtractionResultAsync(extractionResult);

        var message = new OcrTaskPublishMessage
        {
            FileId = request.DocumentVersionId,
            FilePath = request.FilePath,
            BucketName = request.BucketName,
            ProcessOption = request.ProcessOption,
            ExtractPrompt = request.ExtractPrompt,
            Form = form,
            FormSchemaJson = request.FormSchemaJson
        };

        await _messageProducer.PublishToExchangeAsync(message, DigitizationExchange, OcrTaskRoutingKey);

        progress.Status = "Running";
        progress.ModifiedBy = userId;
        progress.ModifiedDate = DateTime.UtcNow;
        await _repository.UpdateProgressAsync(progress);
        NotifyProgressInBackground(dossierId, progress);

        _logger.LogInformation(
            "Đã gửi OCR task version {VersionId}, document {DocumentId}, form {FormId}",
            request.DocumentVersionId, request.DocumentId, request.FormId);

        return MapProgress(progress);
    }

    public async Task HandleProgressMessageAsync(DigitizationProgressMessage message)
    {
        var progress = await _repository.GetProgressByVersionIdAsync(message.FileId);
        if (progress == null)
        {
            _logger.LogWarning("Không tìm thấy OCR progress cho version {VersionId}", message.FileId);
            return;
        }

        progress.Action = message.Action;
        progress.CurrentPage = message.CurrentPage;
        progress.TotalPages = message.TotalPages;
        progress.Progress = message.Progress;
        progress.ModifiedDate = DateTime.UtcNow;

        if (message.Action.Contains("ocr.process.progress", StringComparison.OrdinalIgnoreCase))
        {
            progress.Phase = "ocr";
            progress.Status = message.Progress >= 100 ? "OcrCompleted" : "Running";
        }
        else if (message.Action.Contains("extraction.process.progress", StringComparison.OrdinalIgnoreCase))
        {
            progress.Phase = "extraction";
            progress.Status = "Extracting";
        }

        await _repository.UpdateProgressAsync(progress);

        if (message.Action.Contains("ocr.process.progress", StringComparison.OrdinalIgnoreCase)
            && message.Progress >= 100)
        {
            await TryPublishDocumentTextIndexAsync(
                message.FileId,
                progress.BucketName,
                progress.FilePath,
                progress.TotalPages > 0 ? progress.TotalPages : message.TotalPages,
                "ocr-completed");
        }

        await PublishProgressNotificationAsync(progress);
    }

    public async Task HandleExtractionCompletedAsync(DigitizationExtractionCompletedMessage message)
    {
        var progress = await _repository.GetProgressByVersionIdAsync(message.FileId);
        if (progress != null)
        {
            progress.Action = message.Action;
            progress.Phase = "extraction";
            progress.Progress = 100;
            progress.Status = message.Status.Equals("Success", StringComparison.OrdinalIgnoreCase)
                ? "Completed"
                : "Failed";
            progress.ModifiedDate = DateTime.UtcNow;
            await _repository.UpdateProgressAsync(progress);
        }

        var equipmentId = ResolveEquipmentIdForExtractionCompleted(message);
        var result = await _repository.GetExtractionResultByVersionIdAsync(
            message.FileId,
            equipmentId);
        if (result == null)
        {
            _logger.LogWarning(
                "Không tìm thấy extraction result cho version {VersionId}, equipment {EquipmentId}",
                message.FileId,
                equipmentId);
            return;
        }

        result.Status = message.Status.Equals("Success", StringComparison.OrdinalIgnoreCase)
            ? "Completed"
            : "Failed";
        result.ResultFilePath = message.ResultFile;
        result.BucketName = message.BucketName;
        result.ModifiedDate = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(message.ResultFile) && !string.IsNullOrEmpty(message.BucketName))
        {
            try
            {
                await using var stream = await _fileStorageService.DownloadFileAsync(
                    message.ResultFile, message.BucketName);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                result.ResultJson = await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không tải được JSON kết quả từ MinIO: {Path}", message.ResultFile);
            }
        }

        result.MergedDataJson = ExtractionResultMerger.MergePageResults(result.ResultJson);

        await _repository.UpdateExtractionResultAsync(result);

        // OCR / bóc tách (kể cả bóc tách lại) không tự merge vào FormValues thiết bị —
        // chỉ cập nhật thông số khi user bấm «Cập nhật thông số» trên popup xem tài liệu.

        if (progress != null)
            await PublishProgressNotificationAsync(progress, result.Status);

        if (message.Status.Equals("Success", StringComparison.OrdinalIgnoreCase))
        {
            await TryPublishDocumentTextIndexAsync(
                message.FileId,
                message.BucketName ?? progress?.BucketName,
                progress?.FilePath,
                progress?.TotalPages ?? 0,
                "extraction-completed");
        }
    }

    /// <summary>
    /// Publish index fulltext — gọi ngay sau OCR (file *_page_N.md trên MinIO) và reindex sau bóc tách (cập nhật extractionSummary).
    /// </summary>
    private async Task TryPublishDocumentTextIndexAsync(
        Guid documentVersionId,
        string? bucketName,
        string? filePath,
        int totalPages,
        string trigger)
    {
        try
        {
            var resolvedPath = filePath;
            if (string.IsNullOrEmpty(resolvedPath))
            {
                var version = await _documentRepository.GetDocumentVersionByIdAsync(documentVersionId);
                resolvedPath = version?.FilePath;
            }

            await _documentTextIndexNotifier.PublishIndexAsync(
                documentVersionId,
                bucketName ?? _fileStorageService.DossierBucketName,
                resolvedPath,
                totalPages);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Không publish được sự kiện index ({Trigger}) cho version {VersionId}.",
                trigger,
                documentVersionId);
        }
    }

    private void NotifyProgressInBackground(Guid? dossierId, DocumentOcrProgress progress, string? extractionStatus = null)
    {
        var capturedDossierId = dossierId;
        var capturedProgress = new DocumentOcrProgress
        {
            DocumentId = progress.DocumentId,
            DocumentVersionId = progress.DocumentVersionId,
            Phase = progress.Phase,
            Status = progress.Status,
            Progress = progress.Progress,
            CurrentPage = progress.CurrentPage,
            TotalPages = progress.TotalPages
        };

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var notifier = scope.ServiceProvider.GetRequiredService<IDigitizationProgressNotifier>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<DocumentDigitizationService>>();

                var resolvedDossierId = capturedDossierId
                    ?? await documentRepository.GetDossierIdByVersionIdAsync(capturedProgress.DocumentVersionId);
                if (!resolvedDossierId.HasValue)
                {
                    logger.LogWarning(
                        "Không tìm thấy dossier cho version {VersionId} — bỏ qua push SignalR",
                        capturedProgress.DocumentVersionId);
                    return;
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await notifier.NotifyAsync(new DigitizationProgressPushDto
                {
                    DossierId = resolvedDossierId.Value,
                    DocumentId = capturedProgress.DocumentId,
                    DocumentVersionId = capturedProgress.DocumentVersionId,
                    Phase = capturedProgress.Phase,
                    Status = capturedProgress.Status,
                    Progress = capturedProgress.Progress,
                    CurrentPage = capturedProgress.CurrentPage,
                    TotalPages = capturedProgress.TotalPages,
                    ExtractionStatus = extractionStatus
                }, cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Không push được SignalR progress (background) cho version {VersionId}",
                    capturedProgress.DocumentVersionId);
            }
        });
    }

    private async Task PublishProgressNotificationAsync(DocumentOcrProgress progress, string? extractionStatus = null)
    {
        try
        {
            var dossierId = await _documentRepository.GetDossierIdByVersionIdAsync(progress.DocumentVersionId);
            if (!dossierId.HasValue)
            {
                _logger.LogWarning(
                    "Không tìm thấy dossier cho version {VersionId} — bỏ qua push SignalR",
                    progress.DocumentVersionId);
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await _progressNotifier.NotifyAsync(new DigitizationProgressPushDto
            {
                DossierId = dossierId.Value,
                DocumentId = progress.DocumentId,
                DocumentVersionId = progress.DocumentVersionId,
                Phase = progress.Phase,
                Status = progress.Status,
                Progress = progress.Progress,
                CurrentPage = progress.CurrentPage,
                TotalPages = progress.TotalPages,
                ExtractionStatus = extractionStatus
            }, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Không push được SignalR progress cho version {VersionId}",
                progress.DocumentVersionId);
        }
    }

    public async Task<DocumentOcrProgressDto?> GetProgressForDossierAsync(Guid dossierId, Guid documentVersionId)
    {
        await EnsureVersionInDossierAsync(dossierId, documentVersionId);
        return await GetProgressByVersionIdAsync(documentVersionId);
    }

    public async Task<DocumentExtractionResultDto?> GetExtractionResultForDossierAsync(Guid dossierId, Guid documentVersionId)
    {
        await EnsureVersionInDossierAsync(dossierId, documentVersionId);
        return await GetExtractionResultByVersionIdAsync(documentVersionId);
    }

    public async Task<DocumentExtractionResultDto> SaveDocumentExtractionDataAsync(
        Guid dossierId,
        Guid documentVersionId,
        SaveDocumentExtractionDataRequest request,
        string userId)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.MergedDataJson))
            throw new ArgumentException("Dữ liệu form tài liệu không được để trống.");

        await _dossierService.EnsureCanEditFormDataAsync(dossierId);
        await EnsureVersionInDossierAsync(dossierId, documentVersionId);

        var version = await _documentRepository.GetDocumentVersionByIdAsync(documentVersionId)
            ?? throw new KeyNotFoundException("Phiên bản tài liệu không tồn tại.");

        var result = await _repository.GetExtractionResultByVersionIdAsync(documentVersionId);
        if (result == null)
        {
            result = new DocumentExtractionResult
            {
                DocumentId = version.DocumentId,
                DocumentVersionId = documentVersionId,
                Status = "Manual",
                MergedDataJson = request.MergedDataJson.Trim(),
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow
            };
            await _repository.CreateExtractionResultAsync(result);
        }
        else
        {
            result.MergedDataJson = request.MergedDataJson.Trim();
            result.ModifiedBy = userId;
            result.ModifiedDate = DateTime.UtcNow;
            await _repository.UpdateExtractionResultAsync(result);
        }

        return MapResult(result);
    }

    private async Task EnsureVersionInDossierAsync(Guid dossierId, Guid documentVersionId)
    {
        if (!await _documentRepository.VersionBelongsToDossierAsync(documentVersionId, dossierId))
            throw new KeyNotFoundException("Phiên bản tài liệu không thuộc hồ sơ này.");
    }

    public async Task<DocumentOcrProgressDto?> GetProgressByVersionIdAsync(Guid documentVersionId)
    {
        var progress = await _repository.GetProgressByVersionIdAsync(documentVersionId);
        return progress == null ? null : MapProgress(progress);
    }

    public async Task<DocumentExtractionResultDto?> GetExtractionResultByVersionIdAsync(Guid documentVersionId)
    {
        var result = await _repository.GetExtractionResultByVersionIdAsync(documentVersionId);
        if (result == null) return null;

        if (string.IsNullOrWhiteSpace(result.MergedDataJson) && !string.IsNullOrWhiteSpace(result.ResultJson))
        {
            result.MergedDataJson = ExtractionResultMerger.MergePageResults(result.ResultJson);
            if (!string.IsNullOrWhiteSpace(result.MergedDataJson))
            {
                result.ModifiedDate = DateTime.UtcNow;
                await _repository.UpdateExtractionResultAsync(result);
            }
        }

        return MapResult(result);
    }

    public DigitizationExtractionForm BuildExtractionForm(string formId, string formName, string formSchemaJson)
    {
        var fields = new List<DigitizationExtractionFormField>();

        try
        {
            using var doc = JsonDocument.Parse(formSchemaJson);
            var root = doc.RootElement;

            IEnumerable<JsonElement> fieldElements = root.ValueKind switch
            {
                JsonValueKind.Array => root.EnumerateArray(),
                JsonValueKind.Object when TryGetPropertyIgnoreCase(root, "fields", out var fieldsEl)
                                         && fieldsEl.ValueKind == JsonValueKind.Array
                    => fieldsEl.EnumerateArray(),
                _ => Array.Empty<JsonElement>()
            };

            foreach (var item in fieldElements)
            {
                var fieldName = ResolveSchemaFieldName(item);
                if (string.IsNullOrWhiteSpace(fieldName)) continue;

                var label = ReadSchemaString(item, "label", "Label") ?? fieldName;
                var description = ReadSchemaString(item, "description", "Description") ?? label;
                var placeholder = ReadSchemaString(item, "placeholder", "Placeholder");

                fields.Add(new DigitizationExtractionFormField
                {
                    FieldName = fieldName.Trim(),
                    Description = !string.IsNullOrWhiteSpace(description)
                        ? description!.Trim()
                        : !string.IsNullOrWhiteSpace(placeholder)
                            ? placeholder!.Trim()
                            : label.Trim()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không parse được FormSchemaJson cho form {FormId}", formId);
        }

        if (fields.Count == 0)
        {
            _logger.LogWarning("FormSchema form {FormId} không parse được trường nào (kiểm tra name/key/id).", formId);
        }

        return new DigitizationExtractionForm
        {
            FormId = formId,
            FormName = formName,
            Fields = fields
        };
    }

    /// <summary>
    /// Lấy mã trường EAV — ưu tiên name/key có giá trị, fallback id (form builder hay để name rỗng).
    /// </summary>
    private static string? ResolveSchemaFieldName(JsonElement item)
    {
        foreach (var property in new[] { "name", "Name", "key", "Key", "id", "Id", "fieldName", "FieldName" })
        {
            if (!TryGetPropertyIgnoreCase(item, property, out var el))
                continue;

            var value = ReadJsonScalarAsString(el);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? ReadSchemaString(JsonElement item, params string[] propertyNames)
    {
        foreach (var property in propertyNames)
        {
            if (!TryGetPropertyIgnoreCase(item, property, out var el))
                continue;

            var value = ReadJsonScalarAsString(el);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
            return true;

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ReadJsonScalarAsString(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

    private static DocumentOcrProgressDto MapProgress(DocumentOcrProgress p) => new()
    {
        Id = p.Id,
        DocumentId = p.DocumentId,
        DocumentVersionId = p.DocumentVersionId,
        Action = p.Action,
        Phase = p.Phase,
        CurrentPage = p.CurrentPage,
        TotalPages = p.TotalPages,
        Progress = p.Progress,
        Status = p.Status,
        ProcessOption = p.ProcessOption,
        CreatedDate = p.CreatedDate,
        ModifiedDate = p.ModifiedDate
    };

    private static DocumentExtractionResultDto MapResult(DocumentExtractionResult r) => new()
    {
        Id = r.Id,
        DocumentId = r.DocumentId,
        DocumentVersionId = r.DocumentVersionId,
        Status = r.Status,
        ResultJson = r.ResultJson,
        ResultFilePath = r.ResultFilePath,
        MergedDataJson = r.MergedDataJson,
        EquipmentId = r.EquipmentId,
        CreatedDate = r.CreatedDate,
        ModifiedDate = r.ModifiedDate
    };

    public async Task<DocumentOcrProgressDto> SubmitForEquipmentDocumentAsync(
        Guid equipmentId,
        Guid documentVersionId,
        string userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var equipmentRepository = scope.ServiceProvider.GetRequiredService<IEquipmentRepository>();

        var equipment = await equipmentRepository.GetByIdAsync(equipmentId)
            ?? throw new KeyNotFoundException("Không tìm thấy thiết bị kỹ thuật.");

        var version = await _documentRepository.GetDocumentVersionByIdAsync(documentVersionId)
            ?? throw new KeyNotFoundException("Phiên bản tài liệu không tồn tại.");

        if (string.IsNullOrEmpty(version.FilePath))
            throw new InvalidOperationException("Tài liệu chưa có file để xử lý OCR.");

        var template = await _formTemplateRepository.GetActiveByEquipmentTypeIdAsync(equipment.EquipmentTypeId);
        if (template == null)
            throw new InvalidOperationException("Loại thiết bị chưa gắn biểu mẫu EAV thông số — không thể bóc tách.");

        var form = BuildExtractionForm(template.Id.ToString(), template.Name, template.FormSchema);

        var existing = await _repository.GetProgressByVersionIdAsync(documentVersionId);
        if (existing != null && existing.Status is "Running" or "Extracting" or "Pending")
            throw new InvalidOperationException("Tài liệu đang được xử lý OCR/bóc tách.");

        var bucketName = _fileStorageService.DossierBucketName;

        var progress = new DocumentOcrProgress
        {
            DocumentId = version.DocumentId,
            DocumentVersionId = documentVersionId,
            Phase = "ocr",
            Action = "ocr.process.task",
            Status = "Pending",
            ProcessOption = "OcrAndExtract",
            BucketName = bucketName,
            FilePath = version.FilePath,
            FormJson = template.FormSchema,
            Progress = 0,
            CreatedBy = userId,
            CreatedDate = DateTime.UtcNow
        };
        await _repository.CreateProgressAsync(progress);

        var extractionResult = new DocumentExtractionResult
        {
            DocumentId = version.DocumentId,
            DocumentVersionId = documentVersionId,
            OcrProgressId = progress.Id,
            Status = "Pending",
            FormJson = template.FormSchema,
            BucketName = bucketName,
            CreatedBy = userId,
            CreatedDate = DateTime.UtcNow,
            EquipmentId = equipmentId
        };
        await _repository.CreateExtractionResultAsync(extractionResult);

        var message = new OcrTaskPublishMessage
        {
            FileId = documentVersionId,
            FilePath = version.FilePath ?? string.Empty,
            BucketName = bucketName,
            ProcessOption = "OcrAndExtract",
            ExtractPrompt = template.ExtractionProcess ?? "Hãy đọc văn bản và trích xuất thông tin dưới dạng JSON.",
            Form = form,
            FormSchemaJson = template.FormSchema,
            EquipmentId = equipmentId
        };

        await _messageProducer.PublishToExchangeAsync(message, DigitizationExchange, OcrTaskRoutingKey);

        progress.Status = "Running";
        progress.ModifiedBy = userId;
        progress.ModifiedDate = DateTime.UtcNow;
        await _repository.UpdateProgressAsync(progress);

        return MapProgress(progress);
    }

    public async Task<DocumentOcrProgressDto> ReExtractForEquipmentDocumentAsync(
        Guid equipmentId,
        Guid documentVersionId,
        string userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var equipmentRepository = scope.ServiceProvider.GetRequiredService<IEquipmentRepository>();

        var equipment = await equipmentRepository.GetByIdAsync(equipmentId)
            ?? throw new KeyNotFoundException("Không tìm thấy thiết bị kỹ thuật.");

        var version = await _documentRepository.GetDocumentVersionByIdAsync(documentVersionId)
            ?? throw new KeyNotFoundException("Phiên bản tài liệu không tồn tại.");

        if (string.IsNullOrEmpty(version.FilePath))
            throw new InvalidOperationException("Tài liệu chưa có file để bóc tách.");

        var template = await _formTemplateRepository.GetActiveByEquipmentTypeIdAsync(equipment.EquipmentTypeId);
        if (template == null)
            throw new InvalidOperationException("Loại thiết bị chưa gắn biểu mẫu EAV thông số — không thể bóc tách.");

        var progress = await _repository.GetProgressByVersionIdAsync(documentVersionId)
            ?? throw new InvalidOperationException("Tài liệu chưa qua OCR — không thể bóc tách lại.");

        if (progress.Status is "Running" or "Extracting" or "Pending")
            throw new InvalidOperationException("Tài liệu đang được xử lý — vui lòng đợi hoàn tất.");

        if (!IsOcrPhaseComplete(progress))
            throw new InvalidOperationException("OCR chưa hoàn thành — không thể bóc tách lại.");

        var form = BuildExtractionForm(template.Id.ToString(), template.Name, template.FormSchema);
        var bucketName = _fileStorageService.DossierBucketName;

        progress.Phase = "extraction";
        progress.Action = ExtractionTaskRoutingKey;
        progress.Status = "Extracting";
        progress.Progress = 0;
        progress.CurrentPage = 0;
        progress.TotalPages = 0;
        progress.ProcessOption = "ExtractOnly";
        progress.FormJson = template.FormSchema;
        progress.ModifiedBy = userId;
        progress.ModifiedDate = DateTime.UtcNow;
        await _repository.UpdateProgressAsync(progress);

        var extractionResult = await _repository.GetExtractionResultByVersionIdAsync(documentVersionId, equipmentId);
        if (extractionResult == null)
        {
            extractionResult = new DocumentExtractionResult
            {
                DocumentId = version.DocumentId,
                DocumentVersionId = documentVersionId,
                OcrProgressId = progress.Id,
                Status = "Pending",
                FormJson = template.FormSchema,
                BucketName = bucketName,
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow,
                EquipmentId = equipmentId
            };
            await _repository.CreateExtractionResultAsync(extractionResult);
        }
        else
        {
            extractionResult.Status = "Pending";
            extractionResult.ResultJson = null;
            extractionResult.ResultFilePath = null;
            extractionResult.MergedDataJson = null;
            extractionResult.ErrorMessage = null;
            extractionResult.FormJson = template.FormSchema;
            extractionResult.ModifiedBy = userId;
            extractionResult.ModifiedDate = DateTime.UtcNow;
            await _repository.UpdateExtractionResultAsync(extractionResult);
        }

        var message = new ExtractionTaskPublishMessage
        {
            FileId = documentVersionId,
            FilePath = version.FilePath,
            BucketName = bucketName,
            ExtractPrompt = template.ExtractionProcess ?? "Hãy đọc văn bản và trích xuất thông tin dưới dạng JSON.",
            Form = form,
            FormSchemaJson = template.FormSchema,
            EquipmentId = equipmentId
        };

        await _messageProducer.PublishToExchangeAsync(message, DigitizationExchange, ExtractionTaskRoutingKey);

        await TryPublishDocumentTextIndexAsync(
            documentVersionId,
            bucketName,
            version.FilePath,
            progress.TotalPages,
            "equipment-re-extract-md-ready");

        _logger.LogInformation(
            "Đã gửi bóc tách lại thiết bị {EquipmentId}, version {VersionId}, document {DocumentId}",
            equipmentId, documentVersionId, version.DocumentId);

        return MapProgress(progress);
    }

    public async Task<DocumentExtractionResultDto?> GetExtractionResultForEquipmentAsync(Guid equipmentId, Guid documentVersionId)
    {
        var result = await _repository.GetExtractionResultByVersionIdAsync(documentVersionId, equipmentId);
        if (result == null)
        {
            result = await TryHydrateEquipmentExtractionFromStorageAsync(equipmentId, documentVersionId);
            if (result == null)
                return null;
        }
        else if (NeedsHydrationFromStorage(result))
        {
            await TryHydrateEquipmentExtractionFromStorageAsync(equipmentId, documentVersionId, result);
        }

        if (string.IsNullOrWhiteSpace(result.MergedDataJson) && !string.IsNullOrWhiteSpace(result.ResultJson))
        {
            try
            {
                var parsed = JsonDocument.Parse(result.ResultJson);
                if (parsed.RootElement.TryGetProperty("merged", out var mergedNode))
                {
                    result.MergedDataJson = mergedNode.GetRawText();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi giải nén ResultJson trong GetExtractionResultForEquipmentAsync.");
            }
        }

        return MapResult(result);
    }

    public async Task<DocumentExtractionResultDto> SaveEquipmentExtractionDataAsync(
        Guid equipmentId,
        Guid documentVersionId,
        SaveDocumentExtractionDataRequest request,
        string userId)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.MergedDataJson))
            throw new ArgumentException("Dữ liệu form tài liệu không được để trống.");

        using var scope = _scopeFactory.CreateScope();
        var equipmentRepository = scope.ServiceProvider.GetRequiredService<IEquipmentRepository>();

        var equipment = await equipmentRepository.GetByIdAsync(equipmentId)
            ?? throw new KeyNotFoundException("Không tìm thấy thiết bị kỹ thuật.");

        var version = await _documentRepository.GetDocumentVersionByIdAsync(documentVersionId)
            ?? throw new KeyNotFoundException("Phiên bản tài liệu không tồn tại.");

        var result = await _repository.GetExtractionResultByVersionIdAsync(documentVersionId, equipmentId);
        if (result == null)
        {
            result = new DocumentExtractionResult
            {
                DocumentId = version.DocumentId,
                DocumentVersionId = documentVersionId,
                Status = "Manual",
                MergedDataJson = request.MergedDataJson.Trim(),
                EquipmentId = equipmentId,
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow
            };
            await _repository.CreateExtractionResultAsync(result);
        }
        else
        {
            result.MergedDataJson = request.MergedDataJson.Trim();
            result.ModifiedBy = userId;
            result.ModifiedDate = DateTime.UtcNow;
            await _repository.UpdateExtractionResultAsync(result);
        }

        // Chỉ thay FormValues thiết bị khi user yêu cầu «Cập nhật thông số» (replace toàn bộ).
        if (request.UpdateEquipmentFormValues)
        {
            try
            {
                equipment.FormValues = request.MergedDataJson.Trim();
                equipment.ModifiedBy = userId;
                equipment.ModifiedDate = DateTime.UtcNow;
                await equipmentRepository.UpdateAsync(equipment);
                _logger.LogInformation(
                    "Đã thay thế FormValues thiết bị {EquipmentId} từ kết quả bóc tách version {VersionId}.",
                    equipmentId,
                    documentVersionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi cập nhật FormValues thiết bị {EquipmentId} từ kết quả bóc tách.", equipmentId);
                throw;
            }
        }

        return MapResult(result);
    }

    private static Guid? ResolveEquipmentIdForExtractionCompleted(DigitizationExtractionCompletedMessage message)
    {
        if (message.EquipmentId.HasValue)
            return message.EquipmentId;

        return TryParseEquipmentIdFromResultFile(message.ResultFile);
    }

    private static Guid? TryParseEquipmentIdFromResultFile(string? resultFile)
    {
        if (string.IsNullOrWhiteSpace(resultFile))
            return null;

        var fileName = Path.GetFileName(resultFile);
        const string marker = "_eq_";
        var markerIndex = fileName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return null;

        var equipmentPart = fileName[(markerIndex + marker.Length)..];
        if (equipmentPart.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            equipmentPart = equipmentPart[..^5];

        return Guid.TryParse(equipmentPart, out var equipmentId) ? equipmentId : null;
    }

    private static bool NeedsHydrationFromStorage(DocumentExtractionResult result) =>
        !result.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(result.ResultJson);

    private static string BuildEquipmentExtractionResultPath(string filePath, Guid documentVersionId, Guid equipmentId)
    {
        var directory = Path.GetDirectoryName(filePath)?.Replace("\\", "/") ?? string.Empty;
        var fileName = $"extraction_result_{documentVersionId}_eq_{equipmentId}.json";
        return string.IsNullOrEmpty(directory) ? fileName : $"{directory}/{fileName}";
    }

    private async Task<DocumentExtractionResult?> TryHydrateEquipmentExtractionFromStorageAsync(
        Guid equipmentId,
        Guid documentVersionId,
        DocumentExtractionResult? existing = null)
    {
        var version = await _documentRepository.GetDocumentVersionByIdAsync(documentVersionId);
        if (version == null || string.IsNullOrWhiteSpace(version.FilePath))
            return existing;

        var bucketName = existing?.BucketName ?? _fileStorageService.DossierBucketName;
        var resultPath = BuildEquipmentExtractionResultPath(version.FilePath, documentVersionId, equipmentId);

        try
        {
            await using var stream = await _fileStorageService.DownloadFileAsync(resultPath, bucketName);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var resultJson = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(resultJson))
                return existing;

            if (existing == null)
            {
                existing = new DocumentExtractionResult
                {
                    DocumentId = version.DocumentId,
                    DocumentVersionId = documentVersionId,
                    Status = "Completed",
                    ResultJson = resultJson,
                    ResultFilePath = resultPath,
                    BucketName = bucketName,
                    EquipmentId = equipmentId,
                    CreatedDate = DateTime.UtcNow
                };
                existing.MergedDataJson = ExtractionResultMerger.MergePageResults(resultJson);
                await _repository.CreateExtractionResultAsync(existing);
                _logger.LogInformation(
                    "Đã hydrate extraction result thiết bị {EquipmentId} từ MinIO: {Path}",
                    equipmentId, resultPath);
                return existing;
            }

            existing.Status = "Completed";
            existing.ResultJson = resultJson;
            existing.ResultFilePath = resultPath;
            existing.BucketName = bucketName;
            existing.MergedDataJson = ExtractionResultMerger.MergePageResults(resultJson);
            existing.ModifiedDate = DateTime.UtcNow;
            await _repository.UpdateExtractionResultAsync(existing);
            _logger.LogInformation(
                "Đã cập nhật extraction result thiết bị {EquipmentId} từ MinIO: {Path}",
                equipmentId, resultPath);
            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Không hydrate được extraction result thiết bị {EquipmentId} từ {Path}",
                equipmentId, resultPath);
            return existing;
        }
    }
}
