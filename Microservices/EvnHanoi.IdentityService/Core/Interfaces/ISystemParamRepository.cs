using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface ISystemParamRepository
{
    Task<IEnumerable<SystemParam>> GetAllAsync();
    Task<SystemParam?> GetByKeyAsync(string key);
    Task<bool> UpdateAsync(SystemParam systemParam);
}
