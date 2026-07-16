using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDocumentTypeRepository
{
    Task<DocumentType?> GetByIdAsync(Guid id);
    Task<DocumentType?> GetByCodeAsync(string code);
    Task<(IEnumerable<DocumentType> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword, int? status);
    /// <summary>Loại văn bản đang hoạt động gắn với loại hồ sơ (DOSSIER_TYPE_DOCUMENT_TYPES).</summary>
    Task<IReadOnlyList<DocumentType>> GetActiveByDossierTypeIdAsync(Guid dossierTypeId);
    Task<Guid> CreateAsync(DocumentType documentType);
    Task<bool> UpdateAsync(DocumentType documentType);
    Task<bool> DeleteAsync(Guid id);
}
