namespace EvnHanoi.SyncService.Models;

/// <summary>1 dòng lịch sử gọi PMIS thật — xem Migration0008_CreatePmisApiCallLogTable.</summary>
public class PmisApiCallLog
{
    public string Id { get; set; } = string.Empty;
    public string ApiCode { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? RequestPayload { get; set; }
    public int? StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
    public string? HttpClientName { get; set; }
    public DateTime CalledAt { get; set; }
}
