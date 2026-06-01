using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByUsernameAsync(string username);
    Task<System.Collections.Generic.IEnumerable<User>> GetAllAsync();
    Task<User?> GetByIdAsync(long id);
    Task UpdateAsync(User user);
    Task UpdateFullAsync(User user);
    Task<long> CreateAsync(User user);
    Task DeleteAsync(long id);
    Task<System.Collections.Generic.IEnumerable<string>> GetRolesByUserIdAsync(long userId);
    Task<System.Collections.Generic.IEnumerable<string>> GetPermissionsByUserIdAsync(long userId);
}
