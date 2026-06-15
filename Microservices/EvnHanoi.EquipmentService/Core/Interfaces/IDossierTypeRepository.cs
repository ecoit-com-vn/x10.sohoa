using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDossierTypeRepository
{
    Task<DossierType?> GetByIdAsync(Guid id);
    Task<DossierType?> GetByCodeAsync(string code);
    Task<(IEnumerable<DossierType> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword, int? status);
    Task<Guid> CreateAsync(DossierType dossierType);
    Task<bool> UpdateAsync(DossierType dossierType);
    Task<bool> DeleteAsync(Guid id);
}
