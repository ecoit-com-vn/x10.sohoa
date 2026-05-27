using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByUsernameAsync(string username);
    // Task<IEnumerable<Role>> GetUserRolesAsync(Guid userId);
}
