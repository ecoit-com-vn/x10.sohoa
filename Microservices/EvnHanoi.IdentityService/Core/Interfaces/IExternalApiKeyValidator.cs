namespace EvnHanoi.IdentityService.Core.Interfaces;

public interface IExternalApiKeyValidator
{
    /// <summary>
    /// Validates the key and returns its ID when valid, or null when invalid/expired.
    /// </summary>
    Task<long?> ValidateAsync(string keyName, string keyHash);
}
