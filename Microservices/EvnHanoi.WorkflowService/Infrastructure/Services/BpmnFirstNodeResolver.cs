using EvnHanoi.WorkflowService.Models;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Xml.Linq;

namespace EvnHanoi.WorkflowService.Infrastructure.Services;

public sealed record BpmnFirstNode(string? NodeId, string? NodeName);

/// <summary>
/// Parse node đầu tiên sau startEvent từ BPMN — cache theo definition Id.
/// </summary>
public static class BpmnFirstNodeResolver
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public static BpmnFirstNode Resolve(WorkflowDefinition definition, IMemoryCache cache)
    {
        var cacheKey = $"wf:bpmn:firstnode:{definition.Id}";
        if (cache.TryGetValue(cacheKey, out BpmnFirstNode? cached) && cached != null)
            return cached;

        var result = ParseFromXml(definition.BpmnXml);
        cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    public static void Invalidate(IMemoryCache cache, Guid definitionId) =>
        cache.Remove($"wf:bpmn:firstnode:{definitionId}");

    private static BpmnFirstNode ParseFromXml(string? bpmnXml)
    {
        if (string.IsNullOrEmpty(bpmnXml))
            return new BpmnFirstNode(null, null);

        try
        {
            var xmlDoc = XDocument.Parse(bpmnXml);
            XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
            var process = xmlDoc.Descendants(bpmn + "process").FirstOrDefault();
            if (process == null) return new BpmnFirstNode(null, null);

            var startEvent = process.Elements(bpmn + "startEvent").FirstOrDefault();
            if (startEvent == null) return new BpmnFirstNode(null, null);

            var startEventId = startEvent.Attribute("id")?.Value;
            var outgoingFlow = process.Elements(bpmn + "sequenceFlow")
                .FirstOrDefault(f => f.Attribute("sourceRef")?.Value == startEventId);
            if (outgoingFlow == null) return new BpmnFirstNode(null, null);

            var nextId = outgoingFlow.Attribute("targetRef")?.Value;
            if (string.IsNullOrEmpty(nextId)) return new BpmnFirstNode(null, null);

            var nextNode = process.Elements().FirstOrDefault(e => e.Attribute("id")?.Value == nextId);
            if (nextNode == null) return new BpmnFirstNode(null, null);

            if (nextNode.Name.LocalName.Contains("Gateway", StringComparison.OrdinalIgnoreCase))
            {
                var gwFlow = process.Elements(bpmn + "sequenceFlow")
                    .FirstOrDefault(f => f.Attribute("sourceRef")?.Value == nextId);
                if (gwFlow == null) return new BpmnFirstNode(null, null);

                var targetTaskId = gwFlow.Attribute("targetRef")?.Value;
                if (string.IsNullOrEmpty(targetTaskId)) return new BpmnFirstNode(null, null);

                var targetTask = process.Elements().FirstOrDefault(e => e.Attribute("id")?.Value == targetTaskId);
                if (targetTask == null) return new BpmnFirstNode(null, null);

                return new BpmnFirstNode(targetTaskId, targetTask.Attribute("name")?.Value);
            }

            return new BpmnFirstNode(nextId, nextNode.Attribute("name")?.Value);
        }
        catch
        {
            return new BpmnFirstNode(null, null);
        }
    }
}
