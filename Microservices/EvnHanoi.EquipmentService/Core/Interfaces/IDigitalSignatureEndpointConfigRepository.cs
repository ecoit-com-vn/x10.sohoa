using EvnHanoi.EquipmentService.Core.Models;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDigitalSignatureEndpointConfigRepository
{
    Task<IEnumerable<DigitalSignatureEndpointConfigListItemDto>> GetAllAsync();
    Task<DigitalSignatureEndpointConfig?> GetByApiCodeAsync(string apiCode);
    Task<bool> UpdateAsync(string apiCode, UpdateDigitalSignatureEndpointConfigRequest request, string? modifiedBy);
}
