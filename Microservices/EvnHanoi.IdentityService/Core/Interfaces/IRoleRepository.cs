using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IRoleRepository
{
    Task<IEnumerable<Role>> GetAllAsync();
    Task<Role?> GetByIdAsync(long id);
    Task<long> CreateAsync(Role role);
    Task<bool> UpdateAsync(Role role);
    Task<bool> DeleteAsync(long id);
    Task<IEnumerable<string>> GetPermissionsByRoleIdAsync(long roleId);
    Task<bool> AssignPermissionsToRoleAsync(long roleId, IEnumerable<string> permissionCodes);
}
