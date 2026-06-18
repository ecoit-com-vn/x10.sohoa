using System;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Core.Services;

public class DossierTypeService : IDossierTypeService
{
    private readonly IDossierTypeRepository _dossierTypeRepository;
    private readonly IEavFormTemplateService _eavFormTemplateService;

    public DossierTypeService(
        IDossierTypeRepository dossierTypeRepository,
        IEavFormTemplateService eavFormTemplateService)
    {
        _dossierTypeRepository = dossierTypeRepository;
        _eavFormTemplateService = eavFormTemplateService;
    }

    public async Task<DossierType?> UpdateEavAsync(
        Guid id, 
        Guid? formId, 
        string? formName, 
        string? formCode, 
        string? formCategory, 
        string? formDescription, 
        string? formDescriptionInfo, 
        string? formSchema, 
        string updatedBy)
    {
        var dbItem = await _dossierTypeRepository.GetByIdAsync(id);
        if (dbItem == null)
            return null;

        if (formId == null)
        {
            dbItem.FormId = null;
        }
        else
        {
            // Call form service to update EAV form
            var updatedForm = await _eavFormTemplateService.UpdateFormTemplateAsync(
                formId.Value,
                formName ?? string.Empty,
                formCode ?? string.Empty,
                formCategory ?? string.Empty,
                formDescription ?? string.Empty,
                formDescriptionInfo ?? string.Empty,
                formSchema ?? "[]",
                updatedBy
            );

            dbItem.FormId = updatedForm.Id;
        }

        dbItem.ModifiedBy = updatedBy;
        dbItem.ModifiedDate = DateTime.UtcNow;

        var success = await _dossierTypeRepository.UpdateAsync(dbItem);
        return success ? dbItem : null;
    }
}
