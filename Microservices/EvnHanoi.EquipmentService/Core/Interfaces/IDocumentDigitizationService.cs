using EvnHanoi.EquipmentService.Core.DTOs;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDocumentDigitizationService
{
    Task<DocumentOcrProgressDto> SubmitOcrJobAsync(SubmitDocumentDigitizationRequest request, string userId);
    Task<DocumentOcrProgressDto> SubmitForDossierDocumentAsync(
        Guid dossierId,
        Guid documentVersionId,
        SubmitDossierDocumentDigitizationRequest request,
        string userId);
    /// <summary>Bóc tách lại — gửi thẳng ExtractionWorker, form EAV luôn load mới từ DB.</summary>
    Task<DocumentOcrProgressDto> ReExtractForDossierDocumentAsync(
        Guid dossierId,
        Guid documentVersionId,
        string userId);
    Task HandleProgressMessageAsync(DigitizationProgressMessage message);
    Task HandleExtractionCompletedAsync(DigitizationExtractionCompletedMessage message);
    Task<DocumentOcrProgressDto?> GetProgressByVersionIdAsync(Guid documentVersionId);
    Task<DocumentExtractionResultDto?> GetExtractionResultByVersionIdAsync(Guid documentVersionId);
    Task<DocumentOcrProgressDto?> GetProgressForDossierAsync(Guid dossierId, Guid documentVersionId);
    Task<DocumentExtractionResultDto?> GetExtractionResultForDossierAsync(Guid dossierId, Guid documentVersionId);

    DigitizationExtractionForm BuildExtractionForm(string formId, string formName, string formSchemaJson);
}
