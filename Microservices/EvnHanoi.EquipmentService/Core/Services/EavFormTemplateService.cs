using System;
using System.Text.Json;
using System.Threading.Tasks;
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

    private void ValidateSchema(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            throw new ArgumentException("Cấu trúc Schema không được để trống.");
        }

        try
        {
            using var doc = JsonDocument.Parse(schema);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Cấu trúc Schema phải là một chuỗi JSON hợp lệ. Chi tiết lỗi: {ex.Message}", ex);
        }
    }

    public async Task<EavFormTemplate> CreateFormTemplateAsync(string name, string description, string schema, string createdBy)
    {
        ValidateSchema(schema);

        var template = new EavFormTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description ?? string.Empty,
            Schema = schema,
            Version = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy ?? "admin"
        };

        await _repository.AddAsync(template);
        return template;
    }

    public async Task<EavFormTemplate> UpdateFormTemplateAsync(Guid id, string newName, string newDescription, string newSchema, string updatedBy)
    {
        ValidateSchema(newSchema);

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
