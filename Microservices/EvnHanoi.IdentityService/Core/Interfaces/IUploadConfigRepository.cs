using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IUploadConfigRepository
{
    Task<IEnumerable<UploadConfig>> GetAllAsync();
    Task<UploadConfig?> GetByIdAsync(long id);
    Task<long> CreateAsync(UploadConfig config);
    Task<bool> UpdateAsync(UploadConfig config);
    Task<bool> DeleteAsync(long id);
}
