using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IElasticsearchService
{
    Task CreateIndexAsync();
    Task<IEnumerable<Equipment>> SearchEquipmentsAsync(string keyword, IEnumerable<long>? unitIds = null);
}
