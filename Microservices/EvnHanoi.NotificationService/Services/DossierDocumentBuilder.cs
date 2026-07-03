using System.Globalization;
using System.Text.Json;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.NotificationService.Models;

namespace EvnHanoi.NotificationService.Services;

public class DossierDocumentBuilder : IDossierDocumentBuilder
{
    public DossierEsDocument Build(
        DossierEnrichmentData data,
        IEnumerable<BhsCatalogDefinition> bhsCatalogs,
        IEnumerable<DossierEquipmentEnrichment> equipments)
    {
        var formData = ParseFormData(data.FormDataJson);
        var catalogList = bhsCatalogs.OrderBy(c => c.Priority).ToList();
        var catalogFields = DossierCatalogDataMapper.BuildCatalogFields(formData, catalogList);

        var formFields = formData
            .Select(kv => ToFormField(kv.Key, kv.Value))
            .Where(f => f is not null)
            .Cast<DossierFormFieldEs>()
            .ToList();

        var hasActiveWorkflowTask = HasActiveWorkflowTask(data);

        return new DossierEsDocument
        {
            Id = DossierIndexIdNormalizer.Normalize(data.Id),
            GridTypeId = data.GridTypeId,
            GridTypeName = data.GridTypeName,
            InfrastructureId = data.InfrastructureId,
            InfrastructureName = data.InfrastructureName,
            InfrastructureCode = data.InfrastructureCode,
            UnitId = data.UnitId,
            DossierSetId = data.DossierSetId,
            DossierSetName = data.DossierSetName,
            DossierTypeId = data.DossierTypeId,
            DossierTypeName = data.DossierTypeName,
            StatusId = data.StatusId,
            StatusCode = data.StatusCode,
            StatusName = data.StatusName,
            WorkflowStatusName = data.WorkflowStatusName,
            WorkflowInstanceId = DossierIndexIdNormalizer.NormalizeOrNull(data.WorkflowInstanceId),
            WorkflowInstanceStatus = data.WorkflowInstanceStatus,
            CreatorId = DossierIndexIdNormalizer.NormalizeOrNull(data.CreatorId),
            CreatorUsername = data.CreatorUsername,
            CreatorName = data.CreatorName,
            CreatedDate = data.CreatedDate,
            ModifiedDate = data.ModifiedDate,
            DocumentCount = data.DocumentCount,
            PendingAssignedRoles = data.PendingAssignedRoles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList(),
            PendingAssigneeUserId = DossierIndexIdNormalizer.NormalizeOrNull(data.PendingAssigneeUserId),
            WorkflowParticipantUserIds = data.WorkflowParticipantUserIds
                .Select(id => DossierIndexIdNormalizer.Normalize(id))
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            CurrentStepAllowEdit = data.CurrentStepAllowEdit,
            CurrentVersionNumber = data.CurrentVersionNumber,
            IsDeleted = data.IsDeleted,
            CurrentStepId = hasActiveWorkflowTask ? data.CurrentStepId : null,
            CurrentStepOrder = data.CurrentStepOrder,
            WorkflowLastAction = data.WorkflowLastAction,
            IsReturnedToCreatorStep = data.IsReturnedToCreatorStep,
            CurrentAssignees = hasActiveWorkflowTask && !string.IsNullOrEmpty(data.CurrentAssignees)
                ? data.CurrentAssignees.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
                : new List<string>(),
            AvailableActions = hasActiveWorkflowTask
                ? ParseAvailableActions(data.AvailableActionsJson)
                : new List<WorkflowActionEsDto>(),
            CatalogFields = catalogFields,
            FormFields = formFields,
            Equipments = equipments.Select(e => new DossierEquipmentEs
            {
                EquipmentId = e.EquipmentId,
                EquipmentCode = e.EquipmentCode,
                EquipmentName = e.EquipmentName,
                SerialNumber = e.SerialNumber
            }).ToList(),
            PublishStatusId = data.PublishStatusId,
            PublishStatusCode = data.PublishStatusCode,
            PublishStatusName = data.PublishStatusName,
            KindId = data.KindId,
            KindCode = data.KindCode
        };
    }

    private static bool HasActiveWorkflowTask(DossierEnrichmentData data)
    {
        if (data.StatusId == 6) // Approved
            return false;

        return string.Equals(data.WorkflowInstanceStatus, "Running", StringComparison.OrdinalIgnoreCase);
    }

    private static List<WorkflowActionEsDto> ParseAvailableActions(string? actionsJson)
    {
        if (string.IsNullOrWhiteSpace(actionsJson))
            return new List<WorkflowActionEsDto>();

        try
        {
            return JsonSerializer.Deserialize<List<WorkflowActionEsDto>>(
                actionsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<WorkflowActionEsDto>();
        }
        catch
        {
            return new List<WorkflowActionEsDto>();
        }
    }

    private static Dictionary<string, object?> ParseFormData(string? formDataJson)
    {
        if (string.IsNullOrWhiteSpace(formDataJson))
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(formDataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in doc.RootElement.EnumerateObject())
                result[property.Name] = JsonElementToValue(property.Value);

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static object? JsonElementToValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                    return longValue;
                if (element.TryGetDouble(out var doubleValue))
                    return doubleValue;
                return element.GetRawText();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                return element.GetRawText();
        }
    }

    private static DossierFormFieldEs? ToFormField(string fieldCode, object? rawValue)
    {
        if (rawValue is null)
            return null;

        if (rawValue is JsonElement element)
            return ToFormField(fieldCode, element);

        switch (rawValue)
        {
            case long longValue:
                return new DossierFormFieldEs
                {
                    FieldCode = fieldCode,
                    NumericValue = longValue,
                    TextValue = longValue.ToString(CultureInfo.InvariantCulture)
                };
            case int intValue:
                return new DossierFormFieldEs
                {
                    FieldCode = fieldCode,
                    NumericValue = intValue,
                    TextValue = intValue.ToString(CultureInfo.InvariantCulture)
                };
            case double doubleValue:
                return new DossierFormFieldEs
                {
                    FieldCode = fieldCode,
                    NumericValue = doubleValue,
                    TextValue = doubleValue.ToString(CultureInfo.InvariantCulture)
                };
            case bool boolValue:
                return new DossierFormFieldEs
                {
                    FieldCode = fieldCode,
                    TextValue = boolValue.ToString()
                };
            case string text:
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateValue))
                    return new DossierFormFieldEs { FieldCode = fieldCode, DateValue = dateValue, TextValue = text };
                return new DossierFormFieldEs { FieldCode = fieldCode, TextValue = text };
            default:
                return new DossierFormFieldEs
                {
                    FieldCode = fieldCode,
                    TextValue = FormatValue(rawValue)
                };
        }
    }

    private static DossierFormFieldEs? ToFormField(string fieldCode, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                    return new DossierFormFieldEs { FieldCode = fieldCode, NumericValue = longValue, TextValue = longValue.ToString(CultureInfo.InvariantCulture) };
                if (element.TryGetDouble(out var doubleValue))
                    return new DossierFormFieldEs { FieldCode = fieldCode, NumericValue = doubleValue, TextValue = doubleValue.ToString(CultureInfo.InvariantCulture) };
                return new DossierFormFieldEs { FieldCode = fieldCode, TextValue = element.GetRawText() };
            case JsonValueKind.True:
            case JsonValueKind.False:
                return new DossierFormFieldEs { FieldCode = fieldCode, TextValue = element.GetBoolean().ToString() };
            case JsonValueKind.String:
                var text = element.GetString();
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateValue))
                    return new DossierFormFieldEs { FieldCode = fieldCode, DateValue = dateValue, TextValue = text };
                return new DossierFormFieldEs { FieldCode = fieldCode, TextValue = text };
            default:
                return new DossierFormFieldEs { FieldCode = fieldCode, TextValue = element.GetRawText() };
        }
    }

    private static string FormatValue(object value)
    {
        if (value is JsonElement element)
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True or JsonValueKind.False => element.GetBoolean().ToString(),
                _ => element.GetRawText()
            };

        return value.ToString() ?? string.Empty;
    }
}
