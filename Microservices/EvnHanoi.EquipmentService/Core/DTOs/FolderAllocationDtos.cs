using System;
using System.Text.Json.Serialization;

namespace EvnHanoi.EquipmentService.Core.DTOs;

public class FolderAllocationListItemDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("folder_id")]
    public Guid FolderId { get; set; }

    [JsonPropertyName("folder_name")]
    public string FolderName { get; set; } = string.Empty;

    [JsonPropertyName("folder_path")]
    public string FolderPath { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("user_full_name")]
    public string UserFullName { get; set; } = string.Empty;

    [JsonPropertyName("allocated_date")]
    public DateTime AllocatedDate { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Active";

    [JsonPropertyName("unit_id")]
    public long UnitId { get; set; }

    [JsonPropertyName("unit_name")]
    public string UnitName { get; set; } = string.Empty;
}

public class CreateFolderAllocationRequest
{
    [JsonPropertyName("folder_id")]
    public Guid FolderId { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;
}

public class UpdateFolderAllocationRequest
{
    [JsonPropertyName("folder_id")]
    public Guid FolderId { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;
}

public class FolderLookupItemDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("parent_id")]
    public Guid? ParentId { get; set; }

    [JsonPropertyName("unit_id")]
    public long UnitId { get; set; }

    [JsonPropertyName("unit_code")]
    public string UnitCode { get; set; } = string.Empty;
}

public class UserLookupItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("organization_unit_id")]
    public long? OrganizationUnitId { get; set; }

    [JsonPropertyName("organization_unit_name")]
    public string OrganizationUnitName { get; set; } = string.Empty;
}
