using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDocumentDigitizationRepository
{
    Task<DocumentOcrProgress?> GetProgressByVersionIdAsync(Guid documentVersionId);
    Task<DocumentOcrProgress?> GetProgressByIdAsync(Guid id);
    Task<Guid> CreateProgressAsync(DocumentOcrProgress progress);
    Task<bool> UpdateProgressAsync(DocumentOcrProgress progress);

    Task<DocumentExtractionResult?> GetExtractionResultByVersionIdAsync(Guid documentVersionId, Guid? equipmentId = null);
    Task<Guid> CreateExtractionResultAsync(DocumentExtractionResult result);
    Task<bool> UpdateExtractionResultAsync(DocumentExtractionResult result);
}
