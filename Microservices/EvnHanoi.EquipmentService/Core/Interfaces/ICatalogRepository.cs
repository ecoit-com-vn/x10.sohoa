using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface ICatalogRepository
{
    Task<IEnumerable<Catalog>> GetAllAsync(long? catalogTypeId = null, string? keyword = null, int? status = null, long? unitId = null);
    Task<(IEnumerable<Catalog> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, long? catalogTypeId = null, string? keyword = null, int? status = null, long? unitId = null);
    Task<Catalog?> GetByIdAsync(long id);
    Task<Catalog?> GetByCodeAsync(long catalogTypeId, string code);
    Task<bool> HasChildrenAsync(long id);
    Task<long> CreateAsync(Catalog catalog);
    Task<bool> UpdateAsync(Catalog catalog);
    Task<bool> DeleteAsync(long id);
    Task<IEnumerable<CatalogType>> GetCatalogTypesAsync();
    Task<CatalogType?> GetCatalogTypeByCodeAsync(string code);
    Task<CatalogType?> GetCatalogTypeByIdAsync(long id);
    Task<IEnumerable<CatalogType>> GetCatalogTypesFilteredAsync(bool isPrivate, string? keyword = null, int? status = null);
    Task<CatalogType?> GetCatalogTypeByIdFilteredAsync(long id, bool isPrivate);
    Task<long> CreateCatalogTypeAsync(CatalogType catalogType);
    Task<bool> UpdateCatalogTypeAsync(CatalogType catalogType);
    Task<bool> DeleteCatalogTypeAsync(long id);
    Task<bool> CatalogTypeHasCatalogsAsync(long id);
}
