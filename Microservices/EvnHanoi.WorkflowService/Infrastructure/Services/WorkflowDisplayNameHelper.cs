namespace EvnHanoi.WorkflowService.Infrastructure.Services;

/// <summary>
/// Chọn tên bước hiển thị — ưu tiên tên cấu hình WORKFLOWSTEPS, tránh nhãn BPMN mặc định (1, Activity_xxx).
/// </summary>
public static class WorkflowDisplayNameHelper
{
    public static string Resolve(string? configuredStepName, string? bpmnNodeName, string? nodeId)
    {
        if (!IsWeakLabel(configuredStepName, nodeId))
            return configuredStepName!.Trim();

        if (!IsWeakLabel(bpmnNodeName, nodeId))
            return bpmnNodeName!.Trim();

        return configuredStepName?.Trim()
            ?? bpmnNodeName?.Trim()
            ?? string.Empty;
    }

    public static bool IsWeakLabel(string? value, string? nodeId = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var trimmed = value.Trim();
        if (!string.IsNullOrEmpty(nodeId) &&
            trimmed.Equals(nodeId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (trimmed.All(char.IsDigit))
            return true;

        if (trimmed.StartsWith("Activity_", StringComparison.OrdinalIgnoreCase))
            return true;

        return trimmed.Equals("Running", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Terminated", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Pending", StringComparison.OrdinalIgnoreCase);
    }
}
