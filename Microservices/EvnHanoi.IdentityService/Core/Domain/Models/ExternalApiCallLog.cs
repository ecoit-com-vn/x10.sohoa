namespace EvnHanoi.IdentityService.Core.Domain.Models;

public class ExternalApiCallLog
{
    public long Id { get; set; }
    public long? ApiKeyId { get; set; }
    public string? KeyName { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string? RequestQuery { get; set; }
    public string? RequestIp { get; set; }
    public int? StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public long? DurationMs { get; set; }
    public string? ResponseSummary { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExternalApiCallLogFilter
{
    public string? KeyName { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
