using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEavFormTemplateService
{
    Task<EavFormTemplate> UpdateFormTemplateAsync(Guid id, string newName, string newDescription, string newSchema, string updatedBy);
}
