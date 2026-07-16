using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

using Infrastructure = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;

public interface IInfrastructureRepository
{
    Task<Infrastructure?> GetByIdAsync(Guid id);
    Task<Infrastructure?> GetByCodeAsync(string code);
    Task<(IEnumerable<Infrastructure> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, int infraTypeId, string? keyword, int? status, IEnumerable<long>? unitIds = null, long? unitId = null, int? gridTypeId = null);
    Task<Guid> CreateAsync(Infrastructure infrastructure);
    Task<bool> UpdateAsync(Infrastructure infrastructure);
    Task<bool> DeleteAsync(Guid id);
}
