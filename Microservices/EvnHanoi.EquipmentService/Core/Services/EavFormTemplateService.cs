using System;
using System.Text.Json;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;

namespace EvnHanoi.EquipmentService.Core.Services;

public class EavFormTemplateService : IEavFormTemplateService
{
    private readonly IEavFormTemplateRepository _repository;

    public EavFormTemplateService(IEavFormTemplateRepository repository)
    {
        _repository = repository;
    }

    private void ValidateFormSchema(string formSchema)
    {
        if (string.IsNullOrWhiteSpace(formSchema))
        {
            throw new ArgumentException("Cấu trúc Schema không được để trống.");
        }

        try
        {
            using var doc = JsonDocument.Parse(formSchema);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Cấu trúc Schema phải là một chuỗi JSON hợp lệ. Chi tiết lỗi: {ex.Message}", ex);
        }
    }

    public async Task<EavFormTemplate> CreateFormTemplateAsync(string name, string code, string category, string description, string descriptionInfo, string formSchema, string createdBy)
    {
        ValidateFormSchema(formSchema);

        var template = new EavFormTemplate
        {
            Id = Guid.Parse(UuidHelper.NewUuid()),
            Name = name,
            Code = code,
            Category = category,
            Description = description ?? string.Empty,
            DescriptionInfo = descriptionInfo ?? string.Empty,
            FormSchema = formSchema,
            Version = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy ?? "admin"
        };

        await _repository.AddAsync(template);
        return template;
    }

    public async Task<EavFormTemplate> UpdateFormTemplateAsync(Guid id, string newName, string newCode, string newCategory, string newDescription, string newDescriptionInfo, string newFormSchema, string updatedBy)
    {
        ValidateFormSchema(newFormSchema);

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
            Id = Guid.Parse(UuidHelper.NewUuid()),
            Name = newName,
            Code = newCode,
            Category = newCategory,
            Description = newDescription,
            DescriptionInfo = newDescriptionInfo,
            FormSchema = newFormSchema,
            Version = oldTemplate.Version + 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = updatedBy
        };

        await _repository.AddAsync(newTemplate);

        return newTemplate;
    }
}
