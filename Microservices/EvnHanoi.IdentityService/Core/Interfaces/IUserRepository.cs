using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByUsernameAsync(string username);
    Task<IEnumerable<User>> GetAllAsync();
    Task<User?> GetByIdAsync(string id);
    Task UpdateAsync(User user);
    Task UpdateFullAsync(User user);
    Task<string> CreateAsync(User user);
    Task DeleteAsync(string id);
    Task<IEnumerable<string>> GetRolesByUserIdAsync(string userId);
    Task<IEnumerable<string>> GetPermissionsByUserIdAsync(string userId);
}
