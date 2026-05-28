using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;

namespace EvnHanoi.EquipmentService.Core.Services;

public class EavFormTemplateService : IEavFormTemplateService
{
    private readonly IEavFormTemplateRepository _repository;

    public EavFormTemplateService(IEavFormTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<EavFormTemplate> UpdateFormTemplateAsync(Guid id, string newName, string newDescription, string newSchema, string updatedBy)
    {
        var oldTemplate = await _repository.GetByIdAsync(id);
        if (oldTemplate == null)
        {
            throw new Exception("Template not found");
        }

        // Đổi bản cũ thành IsActive=false
        oldTemplate.IsActive = false;
        await _repository.UpdateAsync(oldTemplate);

        // Tạo bản mới Version+1
        var newTemplate = new EavFormTemplate
        {
            Id = Guid.NewGuid(),
            Name = newName,
            Description = newDescription,
            Schema = newSchema,
            Version = oldTemplate.Version + 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = updatedBy
        };

        await _repository.AddAsync(newTemplate);

        return newTemplate;
    }
}
