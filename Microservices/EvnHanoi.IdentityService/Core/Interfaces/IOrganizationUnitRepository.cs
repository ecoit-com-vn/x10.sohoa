using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IOrganizationUnitRepository
{
    Task<IEnumerable<OrganizationUnit>> GetAllAsync();
    Task<IEnumerable<OrganizationUnit>> GetOrganizationUnitsHierarchicalAsync(long? startUnitId);
    Task<OrganizationUnit?> GetByIdAsync(long id);
    Task<long> CreateAsync(OrganizationUnit unit);
    Task<bool> UpdateAsync(OrganizationUnit unit);
    Task<bool> DeleteAsync(long id);
}
