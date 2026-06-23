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
        string userId)
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
            formSchemaJsonOverride: null,
            extractPromptOverride: null,
            forceReloadFromTemplate: true);

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
        string? formSchemaJsonOverride,
        string? extractPromptOverride,
        bool forceReloadFromTemplate)
    {
        string formId = dossier.FormId?.ToString() ?? string.Empty;
        string formName = dossier.DossierTypeName ?? "Hồ sơ";
        var formSchemaJson = forceReloadFromTemplate ? null : formSchemaJsonOverride?.Trim();
        var extractPrompt = extractPromptOverride?.Trim();

        var documentMeta = await _documentRepository.GetDocumentByIdAsync(documentId);
        if (documentMeta?.DocumentTypeId.HasValue == true)
        {
            var docType = await _documentTypeRepository.GetByIdAsync(documentMeta.DocumentTypeId.Value);
            if (docType?.FormId.HasValue == true)
            {
                formId = docType.FormId.Value.ToString();
                formName = docType.FormName ?? docType.Name;
            }
        }

        EavFormTemplate? template = null;
        if (Guid.TryParse(formId, out var parsedFormId))
            template = await _formTemplateRepository.GetByIdAsync(parsedFormId);

        if (forceReloadFromTemplate || string.IsNullOrWhiteSpace(formSchemaJson))
        {
            if (template == null)
                throw new InvalidOperationException("Loại văn bản chưa gắn form EAV — không thể bóc tách.");

            formSchemaJson = template.FormSchema;
            formId = template.Id.ToString();
            formName = template.Name;
        }

        if (forceReloadFromTemplate || string.IsNullOrWhiteSpace(extractPrompt))
        {
            if (!string.IsNullOrWhiteSpace(template?.ExtractionProcess))
                extractPrompt = template.ExtractionProcess.Trim();
        }

        return new DigitizationFormContext(formId, formName, formSchemaJson!, extractPrompt);
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

        var result = await _repository.GetExtractionResultByVersionIdAsync(message.FileId);
        if (result == null)
        {
            _logger.LogWarning("Không tìm thấy extraction result cho version {VersionId}", message.FileId);
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
        if (progress != null)
            await PublishProgressNotificationAsync(progress, result.Status);
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
        CreatedDate = r.CreatedDate,
        ModifiedDate = r.ModifiedDate
    };
}
