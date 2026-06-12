using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface ICatalogRepository
{
    Task<IEnumerable<Catalog>> GetAllAsync(string? catalogType = null, string? keyword = null, int? status = null, long? unitId = null);
    Task<(IEnumerable<Catalog> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? catalogType = null, string? keyword = null, int? status = null, long? unitId = null);
    Task<Catalog?> GetByIdAsync(long id);
    Task<Catalog?> GetByCodeAsync(string catalogType, string code);
    Task<bool> HasChildrenAsync(long id);
    Task<long> CreateAsync(Catalog catalog);
    Task<bool> UpdateAsync(Catalog catalog);
    Task<bool> DeleteAsync(long id);
    Task<IEnumerable<CatalogType>> GetCatalogTypesAsync();
    Task<CatalogType?> GetCatalogTypeByCodeAsync(string code);
}
