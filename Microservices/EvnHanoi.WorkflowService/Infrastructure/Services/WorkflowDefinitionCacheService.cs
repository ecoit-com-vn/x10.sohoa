using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Infrastructure.Services;

public class WorkflowDefinitionCacheService
{
    private static readonly TimeSpan ActiveDefinitionTtl = TimeSpan.FromMinutes(10);
    private readonly IMemoryCache _cache;
    private readonly IWorkflowRepository _workflowRepository;

    public WorkflowDefinitionCacheService(IMemoryCache cache, IWorkflowRepository workflowRepository)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
    }

    public async Task<WorkflowDefinition?> GetActiveDefinitionByWorkflowTypeIdAsync(int workflowTypeId)
    {
        var cacheKey = $"wf:active:def:{workflowTypeId}";
        if (_cache.TryGetValue(cacheKey, out WorkflowDefinition? cached))
            return cached;

        var definition = await _workflowRepository.GetActiveDefinitionByWorkflowTypeIdAsync(workflowTypeId);
        if (definition != null)
            _cache.Set(cacheKey, definition, ActiveDefinitionTtl);

        return definition;
    }

    public void InvalidateActiveDefinition(int workflowTypeId) =>
        _cache.Remove($"wf:active:def:{workflowTypeId}");
}
