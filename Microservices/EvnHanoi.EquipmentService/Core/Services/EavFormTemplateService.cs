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

    public async Task<EavFormTemplate> CreateFormTemplateAsync(string name, string code, string category, string description, string descriptionInfo, string formSchema, string createdBy, Guid? equipmentTypeId = null, string formType = "FORM", int? gridTypeId = null, string? extractionProcess = null)
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
            ExtractionProcess = extractionProcess,
            EquipmentTypeId = equipmentTypeId,
            GridTypeId = gridTypeId,
            IsActive = true,
            CreatedAt = DateTime.Now,
            CreatedBy = createdBy ?? "admin",
            Status = "Tạo mới",
            FormType = formType
        };

        await _repository.AddAsync(template);

        var version = new EavFormTemplateVersion
        {
            Id = Guid.Parse(UuidHelper.NewUuid()),
            FormTemplateId = template.Id,
            Code = code,
            Name = name,
            Category = category,
            Description = description ?? string.Empty,
            DescriptionInfo = descriptionInfo ?? string.Empty,
            FormSchema = formSchema,
            Version = 1,
            IsActive = true,
            CreatedAt = DateTime.Now,
            CreatedBy = createdBy ?? "admin",
            Status = "Tạo mới"
        };
        await _repository.AddVersionAsync(version);

        template.FormSchema = formSchema;
        template.Version = 1;

        return template;
    }

    public async Task<EavFormTemplate> UpdateFormTemplateAsync(Guid id, string newName, string newCode, string newCategory, string newDescription, string newDescriptionInfo, string newFormSchema, string updatedBy, Guid? equipmentTypeId = null, string formType = "FORM", int? gridTypeId = null, string? extractionProcess = null)
    {
        ValidateFormSchema(newFormSchema);

        var oldTemplate = await _repository.GetByIdAsync(id);
        if (oldTemplate == null)
        {
            throw new Exception("Template not found");
        }

        // 1. Cập nhật metadata của form cha (in-place)
        oldTemplate.Name = newName;
        oldTemplate.Code = newCode;
        oldTemplate.Category = newCategory;
        oldTemplate.Description = newDescription;
        oldTemplate.DescriptionInfo = newDescriptionInfo;
        oldTemplate.ExtractionProcess = extractionProcess;
        oldTemplate.EquipmentTypeId = equipmentTypeId;
        oldTemplate.GridTypeId = gridTypeId;
        oldTemplate.FormType = formType;
        oldTemplate.Status = "Tạo mới"; 

        await _repository.UpdateAsync(oldTemplate);

        // 2. Ngưng toàn bộ phiên bản cũ → tạo phiên bản mới đang sử dụng (luôn chỉ 1 IsActive = 1)
        await _repository.DeactivateVersionsAsync(id);

        var maxVersion = await _repository.GetMaxVersionAsync(id);
        var newVersion = new EavFormTemplateVersion
        {
            Id = Guid.Parse(UuidHelper.NewUuid()),
            FormTemplateId = id,
            Code = newCode,
            Name = newName,
            Category = newCategory,
            Description = newDescription ?? string.Empty,
            DescriptionInfo = newDescriptionInfo ?? string.Empty,
            FormSchema = newFormSchema,
            Version = maxVersion + 1,
            IsActive = true,
            CreatedAt = DateTime.Now,
            CreatedBy = updatedBy,
            Status = "Tạo mới"
        };
        await _repository.AddVersionAsync(newVersion);

        // Gán động các thuộc tính để trả về DTO tương thích ngược
        oldTemplate.FormSchema = newFormSchema;
        oldTemplate.Version = newVersion.Version;

        return oldTemplate;
    }
}
