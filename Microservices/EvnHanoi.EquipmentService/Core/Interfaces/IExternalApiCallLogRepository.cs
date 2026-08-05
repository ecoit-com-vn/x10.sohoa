using EvnHanoi.EquipmentService.Core.DTOs;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IExternalApiCallLogRepository
{
    Task LogAsync(ExternalApiCallLogEntry entry);
}
