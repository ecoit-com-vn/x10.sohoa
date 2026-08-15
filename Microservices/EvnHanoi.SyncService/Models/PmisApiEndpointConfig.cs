namespace EvnHanoi.SyncService.Models;

/// <summary>
/// 1 trong 9 API PMIS theo tài liệu "Phương án đồng bộ PMIS" — Url/Headers do admin cấu hình qua UI,
/// không hard-code trong appsettings.
/// </summary>
public class PmisApiEndpointConfig
{
    public string Id { get; set; } = string.Empty;
    public string ApiCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string HttpMethod { get; set; } = "GET";
    public int? TimeoutSeconds { get; set; }
    public bool IsActive { get; set; }
    public int RowVersion { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public class PmisApiEndpointConfigListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string ApiCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string HttpMethod { get; set; } = "GET";
    public int? TimeoutSeconds { get; set; }
    public bool IsActive { get; set; }
    public int RowVersion { get; set; }
    public int HeaderCount { get; set; }
}

public class UpdatePmisApiEndpointConfigRequest
{
    public string? Url { get; set; }
    public int? TimeoutSeconds { get; set; }
    public bool IsActive { get; set; }
    public int RowVersion { get; set; }
}
