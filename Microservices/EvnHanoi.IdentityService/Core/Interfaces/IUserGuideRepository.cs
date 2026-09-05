using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IUserGuideRepository
{
    Task<IEnumerable<UserGuide>> GetAllAsync();
    Task<UserGuide?> GetByIdAsync(long id);
    Task<long> CreateAsync(UserGuide guide);
    Task<bool> UpdateAsync(UserGuide guide);
    Task<bool> DeleteAsync(long id);
}
