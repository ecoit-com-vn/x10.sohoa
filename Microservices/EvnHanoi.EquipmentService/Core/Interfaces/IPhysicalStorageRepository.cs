using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IPhysicalStorageRepository
{
    // Shelf
    Task<IEnumerable<PhysicalShelf>> GetShelvesAsync(IEnumerable<long>? unitIds = null);
    Task<PhysicalShelf?> GetShelfByIdAsync(long id);
    Task<long> CreateShelfAsync(PhysicalShelf shelf);
    Task<bool> UpdateShelfAsync(PhysicalShelf shelf);
    Task<bool> DeleteShelfAsync(long id);

    // Floor
    Task<IEnumerable<PhysicalFloor>> GetFloorsByShelfIdAsync(long shelfId);
    Task<IEnumerable<PhysicalFloor>> GetFloorsByUnitIdsAsync(IEnumerable<long>? unitIds = null);
    Task<PhysicalFloor?> GetFloorByIdAsync(long id);
    Task<long> CreateFloorAsync(PhysicalFloor floor);
    Task<bool> UpdateFloorAsync(PhysicalFloor floor);
    Task<bool> DeleteFloorAsync(long id);

    // Box
    Task<IEnumerable<PhysicalBox>> GetBoxesByFloorIdAsync(long floorId);
    Task<IEnumerable<PhysicalBox>> GetBoxesByUnitIdsAsync(IEnumerable<long>? unitIds = null);
    Task<PhysicalBox?> GetBoxByIdAsync(long id);
    Task<long> CreateBoxAsync(PhysicalBox box);
    Task<bool> UpdateBoxAsync(PhysicalBox box);
    Task<bool> DeleteBoxAsync(long id);
}
