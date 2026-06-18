using System;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IDossierTypeService
{
    Task<DossierType?> UpdateEavAsync(
        Guid id, 
        Guid? formId, 
        string? formName, 
        string? formCode, 
        string? formCategory, 
        string? formDescription, 
        string? formDescriptionInfo, 
        string? formSchema, 
        string updatedBy);
}
