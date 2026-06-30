// E:\ecoit\sohoax10\sohoa.backend\Microservices\EvnHanoi.IdentityService\Core\Interfaces\IUserGroupRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IUserGroupRepository
{
    Task<IEnumerable<UserGroup>> GetAllAsync();
    Task<(IEnumerable<UserGroup> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword = null, bool? isActive = null);
    Task<UserGroup?> GetByIdAsync(long id);
    Task<long> CreateAsync(UserGroup group);
    Task<bool> UpdateAsync(UserGroup group);
    Task<bool> DeleteAsync(long id);
    
    Task<IEnumerable<User>> GetMembersAsync(long groupId);
    Task<bool> AssignMembersAsync(long groupId, IEnumerable<string> userIds);
    
    Task<IEnumerable<Role>> GetRolesAsync(long groupId);
    Task<bool> AssignRolesAsync(long groupId, IEnumerable<long> roleIds);
}
