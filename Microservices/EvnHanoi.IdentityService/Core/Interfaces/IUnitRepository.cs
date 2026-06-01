using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IUnitRepository
{
    Task<IEnumerable<Unit>> GetAllAsync();
    Task<Unit?> GetByIdAsync(long id);
    Task<long> CreateAsync(Unit unit);
    Task<bool> UpdateAsync(Unit unit);
    Task<bool> DeleteAsync(long id);
}
