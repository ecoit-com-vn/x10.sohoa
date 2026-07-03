using System;
using System.Linq;
using System.Threading.Tasks;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;
using Microsoft.Extensions.Logging;

namespace EvnHanoi.WorkflowService.Infrastructure.Services
{
    public class WorkflowDefinitionService : IWorkflowDefinitionService
    {
        private readonly IWorkflowRepository _workflowRepository;
        private readonly ILogger<WorkflowDefinitionService> _logger;

        public WorkflowDefinitionService(
            IWorkflowRepository workflowRepository,
            ILogger<WorkflowDefinitionService> logger)
        {
            _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<WorkflowDefinition?> UpdateDefinitionWithVersioningAsync(Guid id, WorkflowDefinition dto, string userId)
        {
            var existing = await _workflowRepository.GetDefinitionByIdAsync(id);
            if (existing == null)
            {
                return null;
            }

            var isNameChanged = !string.Equals((existing.Name ?? string.Empty).Trim(), (dto.Name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
            var isBpmnChanged = !string.Equals((existing.BpmnXml ?? string.Empty).Trim(), (dto.BpmnXml ?? string.Empty).Trim(), StringComparison.Ordinal);

            if (isNameChanged || isBpmnChanged)
            {
                // Parse existing version
                int major = 1;
                int minor = 0;
                if (!string.IsNullOrWhiteSpace(existing.Version))
                {
                    var parts = existing.Version.Split('.');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int parsedMajor) && int.TryParse(parts[1], out int parsedMinor))
                    {
                        major = parsedMajor;
                        minor = parsedMinor;
                    }
                    else if (parts.Length == 1 && int.TryParse(parts[0], out int singleMajor))
                    {
                        major = singleMajor;
                        minor = 0;
                    }
                }

                if (isNameChanged)
                {
                    dto.Version = $"{major + 1}.0";
                }
                else
                {
                    dto.Version = $"{major}.{minor + 1}";
                }

                // Write as a NEW record
                dto.Id = Guid.CreateVersion7();
                dto.CreatedAt = DateTime.UtcNow;
                dto.UpdatedAt = DateTime.UtcNow;
                dto.CreatedBy = userId;
                dto.UpdatedBy = userId;

                if (dto.Steps != null)
                {
                    foreach (var step in dto.Steps)
                    {
                        step.Id = Guid.CreateVersion7();
                        step.WorkflowDefinitionId = dto.Id;
                    }
                }

                dto.IsActive = true;
                dto.ForceActivate = true;

                var success = await _workflowRepository.CreateDefinitionAsync(dto);
                if (!success)
                {
                    throw new InvalidOperationException("Không thể tạo phiên bản quy trình mới.");
                }

                _logger.LogInformation("Quy trình tạo phiên bản mới: {Name} v{Version}", dto.Name, dto.Version);
                return await _workflowRepository.GetDefinitionByIdAsync(dto.Id);
            }
            else
            {
                // In-place update
                dto.UpdatedAt = DateTime.UtcNow;
                dto.UpdatedBy = userId;

                if (dto.Steps != null)
                {
                    foreach (var step in dto.Steps)
                    {
                        step.WorkflowDefinitionId = id;
                    }
                }

                var success = await _workflowRepository.UpdateDefinitionAsync(id, dto);
                if (!success)
                {
                    throw new InvalidOperationException("Không thể cập nhật quy trình.");
                }

                _logger.LogInformation("Quy trình cập nhật: {Name} v{Version}", dto.Name, dto.Version);
                return await _workflowRepository.GetDefinitionByIdAsync(id);
            }
        }

        public async Task<WorkflowDefinition?> GetLatestActiveDefinitionByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var activeDefs = await _workflowRepository.GetAllDefinitionsAsync(name, true);
            return activeDefs
                .Where(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && d.IsActive)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefault();
        }

        public async Task<WorkflowDefinition?> GetDefinitionByIdAsync(Guid id)
        {
            return await _workflowRepository.GetDefinitionByIdAsync(id);
        }

        public async Task<bool> ReactivateDefinitionAsync(Guid id, int workflowTypeId, string name)
        {
            return await _workflowRepository.ReactivateDefinitionAsync(id, workflowTypeId, name);
        }
    }
}
