using EvnHanoi.EquipmentService.Core.DTOs;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDossierSearchRepository
{
    Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(DossierFilterDto filter);
}
