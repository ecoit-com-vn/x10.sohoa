using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByUsernameAsync(string username);
    Task<IEnumerable<User>> GetAllAsync();
    Task<(IEnumerable<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword = null, long? organizationUnitId = null, bool? isActive = null, bool includeDescendants = false);
    Task<User?> GetByIdAsync(string id);
    Task UpdateAsync(User user);
    Task UpdateFullAsync(User user);
    Task UpdateProfileAsync(User user);
    Task UpdateAvatarAsync(string userId, string? avatarObjectKey);
    Task UpdatePasswordAsync(string userId, string passwordHash);
    Task<bool> EmailExistsForOtherUserAsync(string email, string userId);
    Task<string> CreateAsync(User user);
    Task DeleteAsync(string id);
    Task<IEnumerable<string>> GetRolesByUserIdAsync(string userId);
    Task<IEnumerable<string>> GetPermissionsByUserIdAsync(string userId);
    Task<IEnumerable<long>> GetDirectRoleIdsByUserIdAsync(string userId);
    Task<bool> AssignRolesToUserAsync(string userId, IEnumerable<long> roleIds);
    Task<IEnumerable<UserLookupDto>> GetUsersLookupAsync(string? roleCodeFilter);
}
