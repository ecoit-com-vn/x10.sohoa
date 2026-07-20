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

    private static bool AreJsonSchemasEqual(string? json1, string? json2)
    {
        if (string.IsNullOrWhiteSpace(json1) && string.IsNullOrWhiteSpace(json2)) return true;
        if (string.IsNullOrWhiteSpace(json1) || string.IsNullOrWhiteSpace(json2)) return false;
        if (string.Equals(json1.Trim(), json2.Trim(), StringComparison.Ordinal)) return true;

        try
        {
            using var doc1 = JsonDocument.Parse(json1);
            using var doc2 = JsonDocument.Parse(json2);
            return JsonElement.DeepEquals(doc1.RootElement, doc2.RootElement);
        }
        catch
        {
            return false;
        }
    }

    public async Task<EavFormTemplate> CreateFormTemplateAsync(string name, string code, string category, string description, string descriptionInfo, string formSchema, string createdBy, Guid? equipmentTypeId = null, string formType = "FORM", int? gridTypeId = null, string? extractionProcess = null, string? extractionPosition = null)
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
            ExtractionPosition = extractionPosition,
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

    public async Task<EavFormTemplate> UpdateFormTemplateAsync(Guid id, string newName, string newCode, string newCategory, string newDescription, string newDescriptionInfo, string newFormSchema, string updatedBy, Guid? equipmentTypeId = null, string formType = "FORM", int? gridTypeId = null, string? extractionProcess = null, string? extractionPosition = null)
    {
        ValidateFormSchema(newFormSchema);

        var oldTemplate = await _repository.GetByIdAsync(id);
        if (oldTemplate == null)
        {
            throw new Exception("Template not found");
        }

        // Kiểm tra xem có sự thay đổi nào giữa nội dung mới và phiên bản đang active hiện tại không
        bool nameChanged = !string.Equals(oldTemplate.Name, newName ?? string.Empty, StringComparison.Ordinal);
        bool codeChanged = !string.Equals(oldTemplate.Code, newCode ?? string.Empty, StringComparison.Ordinal);
        bool categoryChanged = !string.Equals(oldTemplate.Category, newCategory ?? string.Empty, StringComparison.Ordinal);
        bool descriptionChanged = !string.Equals(oldTemplate.Description ?? string.Empty, newDescription ?? string.Empty, StringComparison.Ordinal);
        bool descriptionInfoChanged = !string.Equals(oldTemplate.DescriptionInfo ?? string.Empty, newDescriptionInfo ?? string.Empty, StringComparison.Ordinal);
        bool extractionProcessChanged = !string.Equals(oldTemplate.ExtractionProcess ?? string.Empty, extractionProcess ?? string.Empty, StringComparison.Ordinal);
        bool extractionPositionChanged = !string.Equals(oldTemplate.ExtractionPosition ?? string.Empty, extractionPosition ?? string.Empty, StringComparison.Ordinal);
        bool equipmentTypeIdChanged = oldTemplate.EquipmentTypeId != equipmentTypeId;
        bool gridTypeIdChanged = oldTemplate.GridTypeId != gridTypeId;
        bool formTypeChanged = !string.Equals(oldTemplate.FormType, formType, StringComparison.Ordinal);
        bool schemaChanged = !AreJsonSchemasEqual(oldTemplate.FormSchema, newFormSchema);

        bool isChanged = nameChanged || codeChanged || categoryChanged || descriptionChanged
            || descriptionInfoChanged || extractionProcessChanged || extractionPositionChanged
            || equipmentTypeIdChanged || gridTypeIdChanged || formTypeChanged || schemaChanged;

        // Nếu nội dung form hoàn toàn không đổi -> Không tạo phiên bản mới, giữ nguyên form hiện tại
        if (!isChanged)
        {
            return oldTemplate;
        }

        // Nếu có thay đổi -> Xác định trạng thái của phiên bản mới
        // Bảo toàn trạng thái "Hoàn thành" nếu form cũ đã Hoàn thành (hoặc chưa set status)
        string targetStatus = string.Equals(oldTemplate.Status, "Hoàn thành", StringComparison.OrdinalIgnoreCase)
                              || string.IsNullOrWhiteSpace(oldTemplate.Status)
            ? "Hoàn thành"
            : oldTemplate.Status;

        // 1. Cập nhật metadata của form cha (in-place)
        oldTemplate.Name = newName ?? string.Empty;
        oldTemplate.Code = newCode ?? string.Empty;
        oldTemplate.Category = newCategory ?? string.Empty;
        oldTemplate.Description = newDescription ?? string.Empty;
        oldTemplate.DescriptionInfo = newDescriptionInfo ?? string.Empty;
        oldTemplate.ExtractionProcess = extractionProcess;
        oldTemplate.ExtractionPosition = extractionPosition;
        oldTemplate.EquipmentTypeId = equipmentTypeId;
        oldTemplate.GridTypeId = gridTypeId;
        oldTemplate.FormType = formType;
        oldTemplate.Status = targetStatus;

        await _repository.UpdateAsync(oldTemplate);

        // 2. Ngưng toàn bộ phiên bản cũ → tạo phiên bản mới đang sử dụng (luôn chỉ 1 IsActive = 1)
        await _repository.DeactivateVersionsAsync(id);

        var maxVersion = await _repository.GetMaxVersionAsync(id);
        var newVersion = new EavFormTemplateVersion
        {
            Id = Guid.Parse(UuidHelper.NewUuid()),
            FormTemplateId = id,
            Code = newCode ?? string.Empty,
            Name = newName ?? string.Empty,
            Category = newCategory ?? string.Empty,
            Description = newDescription ?? string.Empty,
            DescriptionInfo = newDescriptionInfo ?? string.Empty,
            ExtractionPosition = extractionPosition,
            FormSchema = newFormSchema,
            Version = maxVersion + 1,
            IsActive = true,
            CreatedAt = DateTime.Now,
            CreatedBy = updatedBy,
            Status = targetStatus
        };
        await _repository.AddVersionAsync(newVersion);

        // Gán động các thuộc tính để trả về DTO tương thích ngược
        oldTemplate.FormSchema = newFormSchema;
        oldTemplate.Version = newVersion.Version;

        return oldTemplate;
    }
}
