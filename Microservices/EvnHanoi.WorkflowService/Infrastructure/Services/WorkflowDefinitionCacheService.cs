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

    public async Task<WorkflowDefinition?> GetActiveDefinitionByEntityTypeAsync(string entityType)
    {
        var cacheKey = $"wf:active:def:{entityType}";
        if (_cache.TryGetValue(cacheKey, out WorkflowDefinition? cached))
            return cached;

        var definition = await _workflowRepository.GetActiveDefinitionByEntityTypeAsync(entityType);
        if (definition != null)
            _cache.Set(cacheKey, definition, ActiveDefinitionTtl);

        return definition;
    }

    public void InvalidateActiveDefinition(string entityType) =>
        _cache.Remove($"wf:active:def:{entityType}");
}
