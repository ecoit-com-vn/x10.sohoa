using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IExternalApiKeyRepository
{
    Task<IEnumerable<ExternalApiKey>> GetAllAsync();
    Task<ExternalApiKey?> GetByIdAsync(long id);
    Task<long> CreateAsync(ExternalApiKey apiKey);
    Task<bool> UpdateAsync(ExternalApiKey apiKey);
    Task<bool> UpdateKeyValueAsync(long id, string keyHash, string encryptedKey);
    Task<bool> DeleteAsync(long id);
}
