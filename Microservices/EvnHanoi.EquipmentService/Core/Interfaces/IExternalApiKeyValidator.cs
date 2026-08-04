namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IExternalApiKeyValidator
{
    Task<bool> IsValidAsync(string keyName, string keyHash);
}
