using System;
using System.Collections.Generic;

namespace EvnHanoi.SyncService.Models;

public class PmisPushPayload
{
    public string EquipmentId { get; set; } = string.Empty;
    public string EquipmentCode { get; set; } = string.Empty;
    public Dictionary<string, string> Specifications { get; set; } = new();
    public string FactoryTestReportId { get; set; } = string.Empty;
    public string FactoryTestReportStatus { get; set; } = string.Empty;
    public Dictionary<string, object> CbmData { get; set; } = new();
    public DateTime SyncedAt { get; set; }
}
