using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface ICatalogRepository
{
    Task<IEnumerable<Catalog>> GetAllAsync(long? unitId = null);
    Task<Catalog?> GetByIdAsync(long id);
    Task<long> CreateAsync(Catalog catalog);
    Task<bool> UpdateAsync(Catalog catalog);
    Task<bool> DeleteAsync(long id);
}
