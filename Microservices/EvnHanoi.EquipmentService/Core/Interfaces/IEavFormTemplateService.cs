using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEavFormTemplateService
{
    Task<EavFormTemplate> CreateFormTemplateAsync(string name, string description, string schema, string createdBy);
    Task<EavFormTemplate> UpdateFormTemplateAsync(Guid id, string newName, string newDescription, string newSchema, string updatedBy);
}
