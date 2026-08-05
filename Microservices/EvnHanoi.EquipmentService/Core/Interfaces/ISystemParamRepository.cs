namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface ISystemParamRepository
{
    Task<string?> GetValueAsync(string paramKey);
}
