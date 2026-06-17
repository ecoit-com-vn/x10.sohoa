using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IEavFormTemplateService
{
    Task<EavFormTemplate> CreateFormTemplateAsync(string name, string code, string category, string description, string descriptionInfo, string formSchema, string createdBy, Guid? equipmentTypeId = null, string formType = "FORM");
    Task<EavFormTemplate> UpdateFormTemplateAsync(Guid id, string newName, string newCode, string newCategory, string newDescription, string newDescriptionInfo, string newFormSchema, string updatedBy, Guid? equipmentTypeId = null, string formType = "FORM");
}
