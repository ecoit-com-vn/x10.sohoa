using EvnHanoi.EquipmentService.Core.DTOs;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDigitizationProgressNotifier
{
    Task NotifyAsync(DigitizationProgressPushDto payload, CancellationToken cancellationToken = default);
}
